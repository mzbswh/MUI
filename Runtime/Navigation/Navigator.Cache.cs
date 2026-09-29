using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly int cacheCapacity;
        // 按最近归还顺序排列；命中移出，下一次关闭重新放到末尾。
        private readonly List<CachedContent> cachedContents = new List<CachedContent>();
        private Task cacheClearing;
        private Task<Exception> cacheRetiring;
        private int retiringCachedViews;
        private long lastCacheSweep;
        private readonly List<Exception> cacheReleaseErrors = new List<Exception>();
        private long omittedCacheReleaseErrors;

        // 同步缓存以直接状态判忙，不借助任务状态推断清理是否结束。
        private bool IsCacheClearing => Mode == LifetimeMode.Synchronous
            ? synchronousCacheClearing : cacheClearing != null && !cacheClearing.IsCompleted;

        private bool IsCacheRetiring => Mode == LifetimeMode.Synchronous
                    ? retiringCachedViews != 0 : cacheRetiring != null && !cacheRetiring.IsCompleted;

        /// <summary>缓存目录条目数；过期项在命中时不可复用，并由维护扫描移出。</summary>
        public int CachedViewCount
        {
            get
            {
                AssertThread();
                return cachedContents.Count;
            }
        }

        /// <summary>已不可命中但尚未完成最终释放的缓存实例数。</summary>
        public int RetiringCachedViewCount
        {
            get
            {
                AssertThread();
                return retiringCachedViews;
            }
        }

        private static bool CacheExpired(CachedContent entry, long now) =>
                    entry.Route.Policy.CacheMode == ViewCacheMode.Timed &&
                    (now - entry.CachedAt) / (double)Stopwatch.Frequency >= entry.Route.Policy.CacheDuration.Value.TotalSeconds;

        private bool HasCachedContent<TViewModel>(Route route, TViewModel assigned) where TViewModel : ViewModel
        {
            var providerVersion = CaptureProviderVersion();
            var now = Stopwatch.GetTimestamp();
            return assigned == null && cachedContents.Exists(entry =>
                ReferenceEquals(entry.Route, route) && IsCacheCompatible(entry.Content, providerVersion) && !CacheExpired(entry, now));
        }

        internal ViewContent<TViewModel, TArgs, TResult> TakeCachedContent<TViewModel, TArgs, TResult>(
                    Route<TViewModel, TArgs, TResult> route) where TViewModel : ViewModel
        {
            AssertThread();
            var providerVersion = CaptureProviderVersion();
            var now = Stopwatch.GetTimestamp();
            for (var index = cachedContents.Count - 1; index >= 0; --index)
            {
                var entry = cachedContents[index];
                if (ReferenceEquals(entry.Route, route) && IsCacheCompatible(entry.Content, providerVersion) && !CacheExpired(entry, now))
                {
                    cachedContents.RemoveAt(index);
                    reservedCacheEstimatedBytes -= entry.Route.EstimatedRetainedBytes ?? 0;
                    return (ViewContent<TViewModel, TArgs, TResult>)entry.Content;
                }
            }

            return null;
        }

        internal bool RetainContent<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
                    ViewContent<TViewModel, TArgs, TResult> content) where TViewModel : ViewModel
        {
            AssertThread();
            var providerVersion = CaptureProviderVersion();
            if (IsShutdown || synchronousCacheClearing || IsClearingInactiveContent || IsCacheClearing || cacheCapacity == 0 || !IsCacheCompatible(content, providerVersion))
            {
                return false;
            }

            if (!TryMakeCacheRoom(route))
            {
                return false;
            }

            // 淘汰回调可能重建绑定注册表，准入检查必须覆盖回调后的代际。
            providerVersion = CaptureProviderVersion();
            if (IsShutdown || IsCacheClearing || !IsCacheCompatible(content, providerVersion))
            {
                return false;
            }

            content.Lifecycle.RebindHost(InvokeCachedCallback, InvokeCachedCallbackAsync);
            cachedContents.Add(new CachedContent { Route = route, Content = content, CachedAt = Stopwatch.GetTimestamp() });
            reservedCacheEstimatedBytes += route.EstimatedRetainedBytes ?? 0;
            return true;
        }

        /// <summary>无帧驱动的宿主可显式清理过期项；不等待物理释放，不关闭活动页面。</summary>
        private void RefreshCacheUntraced()
        {
            AssertThread();
            if (IsShutdown || IsReentrant || IsSourceCommandRunning || HasCloseEvaluation)
            {
                return;
            }

            SweepExpiredCache();
        }

        private void SweepExpiredCache()
        {
            if (IsCacheClearing || IsCacheRetiring)
            {
                return;
            }

            var providerVersion = CaptureProviderVersion();
            var now = Stopwatch.GetTimestamp();
            var expired = new List<CachedContent>();
            for (var index = cachedContents.Count - 1; index >= 0; --index)
            {
                if (!IsCacheCompatible(cachedContents[index].Content, providerVersion) || CacheExpired(cachedContents[index], now))
                {
                    expired.Add(cachedContents[index]);
                    cachedContents.RemoveAt(index);
                }
            }

            if (expired.Count != 0)
            {
                BeginCacheRetirement(expired.ToArray());
            }
        }

        private void TickCache()
        {
            var now = Stopwatch.GetTimestamp();
            if ((now - lastCacheSweep) / (double)Stopwatch.Frequency >= 1)
            {
                lastCacheSweep = now;
                try
                {
                    PruneTerminals(now);
                    RefreshCacheUntraced();
                    if (Mode == LifetimeMode.AsyncAllowed)
                    {
                        RefreshPreloadProviderVersion();
                    }
                    else
                    {
                        RefreshSynchronousPreloadVersion();
                    }
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        private void BeginCacheRetirement(CachedContent[] saved)
        {
            if (Mode == LifetimeMode.Synchronous)
            {
                var error = ReleaseCachedContentsSynchronous(saved);
                if (error != null)
                {
                    UIErrors.Report(error);
                }
                return;
            }

            var completion = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            cacheRetiring = completion.Task;
            retiringCachedViews += saved.Length;
            _ = ReleaseCachedContentsAsync(saved, completion);
            _ = ReportCacheRetirementAsync(completion.Task);
        }

        private static async Task ReportCacheRetirementAsync(Task<Exception> operation)
        {
            var error = await operation;
            if (error != null)
            {
                UIErrors.Report(error);
            }
        }

        private void InvokeCachedCallback(Action action)
        {
            using (EnterCallback(null))
            {
                action();
            }
        }

        private async ValueTask InvokeCachedCallbackAsync(Func<ValueTask> action)
        {
            using (EnterCallback(null))
            {
                await action();
            }
        }

        /// <summary>移出缓存并等待在途淘汰及最终销毁；不关闭活动页面，重复调用共享清理。</summary>
        private ValueTask ClearCacheAsyncUntraced()
        {
            AssertThread();
            RequireAsyncNavigation();
            if (IsReentrant || IsSourceCommandRunning || HasCloseEvaluation)
            {
                throw new InvalidOperationException("Cannot await cache clearing from navigation callbacks.");
            }

            return new ValueTask(BeginCacheClear());
        }

        private Task BeginCacheClear()
        {
            RequireAsyncNavigation();
            if (IsCacheClearing)
            {
                return cacheClearing;
            }

            var saved = cachedContents.ToArray();
            cachedContents.Clear();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            cacheClearing = completion.Task;
            retiringCachedViews += saved.Length;
            _ = FinishCacheClearAsync(saved, cacheRetiring, completion);
            return cacheClearing;
        }

        private async Task FinishCacheClearAsync(CachedContent[] saved, Task<Exception> retirement,
                    TaskCompletionSource<bool> completion)
        {
            var release = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            // 同时释放已移出的目录内容，不能让一个旧淘汰阻止其他无依赖实例收尾。
            _ = ReleaseCachedContentsAsync(saved, release);
            var failure = await release.Task;
            var priorFailure = retirement == null ? null : await retirement;
            // 清理已观察过的淘汰结果不污染下一次清缓存；历史错误仍保留到宿主退出。
            if (ReferenceEquals(cacheRetiring, retirement))
            {
                cacheRetiring = null;
            }

            if (failure == null && priorFailure == null)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(failure == null ? priorFailure : priorFailure == null ? failure :
                    new AggregateException("Navigation cache clearing failed.", priorFailure, failure));
            }
        }

        private async Task ReleaseCachedContentsAsync(CachedContent[] saved, TaskCompletionSource<Exception> completion)
        {
            var errors = new List<Exception>();
            var trace = CurrentTraceOperation;
            foreach (var entry in saved)
            {
                var errorsBeforeRelease = errors.Count;
                try
                {
                    // 实例资源和凭证释放也会执行外部代码，不能在回调中等待自身清理。
                    using (EnterCallback(null))
                    {
                        await entry.Content.ReleaseCachedAsync(errors,
                            CreateResourceReleaseTrace(default, entry.Route.Key, trace));
                    }
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                finally
                {
                    retiringCachedViews--;
                    var estimate = entry.Route.EstimatedRetainedBytes ?? 0;
                    if (errors.Count == errorsBeforeRelease)
                    {
                        reservedCacheEstimatedBytes -= estimate;
                    }
                    else
                    {
                        // 无法证明释放成功时不归还额度，避免错误路径重复透支。
                        failedCacheEstimatedBytes += estimate;
                    }
                }
            }

            if (errors.Count == 0)
            {
                completion.TrySetResult(null);
            }
            else
            {
                var failure = new AggregateException("Navigation cache cleanup failed.", errors);
                if (cacheReleaseErrors.Count < terminalCapacity)
                {
                    cacheReleaseErrors.Add(failure);
                }
                else if (omittedCacheReleaseErrors < long.MaxValue)
                {
                    omittedCacheReleaseErrors++;
                }

                completion.TrySetResult(failure);
            }
        }

        private bool IsRecordedCacheFailure(Exception failure)
        {
            if (cacheReleaseErrors.Contains(failure))
            {
                return true;
            }

            return failure is AggregateException aggregate && aggregate.InnerExceptions.Count != 0 &&
                System.Linq.Enumerable.All(aggregate.InnerExceptions, IsRecordedCacheFailure);
        }

        private sealed class CachedContent
        {
            public Route Route;
            public ViewContent Content;
            public long CachedAt;
        }
    }
}
