using System;
using System.Collections.Generic;
using System.Linq;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private bool synchronousShuttingDown;
        private bool synchronousShutdownCompleted;
        private Exception synchronousShutdownError;
        private bool synchronousCacheClearing;
        private readonly HashSet<ViewHandle> synchronousCloseRequests = new HashSet<ViewHandle>();

        /// <summary>
        /// 创建纯同步宿主。提供方无需实现异步接口；路由必须声明完整同步生命周期。
        /// 打开时直接完成进入转场，关闭时直接释放，延后请求仅由 Pump 同步派发。
        /// </summary>
        public static Navigator CreateSynchronous(ISynchronousViewProvider provider,
            int queueCapacity = 64, int terminalCapacity = 256,
            UIUserPreferences userPreferences = null, int cacheCapacity = 16,
            long? maxCachedEstimatedBytes = null,
            int preloadCapacity = 32)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            return new Navigator(null, provider, queueCapacity, terminalCapacity, preloadCapacity, null,
                userPreferences, cacheCapacity, maxCachedEstimatedBytes, 1);
        }

        /// <summary>回调或命令尚未退出时，交给下一次同步帧泵；不创建异步工作。</summary>
        private void RequestSynchronousClose(ViewInstance instance, DismissReason reason, Action acceptResult = null)
        {
            if (HasCloseEvaluation)
            {
                throw new InvalidOperationException("A close guard cannot recursively request navigation close.");
            }
            if (!instance.CanReleaseSynchronously)
            {
                if (synchronousCloseRequests.Contains(instance.Handle))
                {
                    return;
                }
                if (posted.Count >= queueCapacity)
                {
                    throw new InvalidOperationException("Synchronous navigation request queue is full.");
                }
                synchronousCloseRequests.Add(instance.Handle);
                posted.Enqueue(() =>
                {
                    synchronousCloseRequests.Remove(instance.Handle);
                    if (entries.ContainsKey(instance.Handle) && instance.IsActive)
                    {
                        RequestSynchronousClose(instance, reason, acceptResult);
                    }
                });
                return;
            }

            var outcome = BeginRequestedCloseSynchronous(instance, reason, acceptResult);
            if (outcome.Error != null)
            {
                UIErrors.Report(outcome.Error);
            }
        }

        private void RequireAsyncNavigation()
        {
            if (Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("A synchronous navigator cannot execute asynchronous navigation.");
            }
        }

        private void RequireSynchronousNavigation()
        {
            AssertThread();
            if (Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("This operation requires a synchronous navigator.");
            }
        }

        private CleanupStatus CloseAfterFailure(ViewInstance instance, DismissReason reason)
        {
            if (Mode == LifetimeMode.Synchronous)
            {
                return BeginCloseSynchronous(instance, reason).Cleanup;
            }

            var cleanup = BeginClose(instance, reason);
            Observe(cleanup);
            return CleanupState(cleanup);
        }

        /// <summary>立即移出并销毁全部缓存；继续清理其余条目后统一报告错误。</summary>
        public void ClearCache()
        {
            RequireSynchronousNavigation();
            if (IsReentrant || IsSourceCommandRunning || HasCloseEvaluation || synchronousCacheClearing)
            {
                throw new InvalidOperationException("Cannot clear synchronous cache from navigation callbacks.");
            }

            ClearCacheSynchronousCore();
        }

        private void ClearCacheSynchronousCore()
        {
            synchronousCacheClearing = true;
            try
            {
                var saved = cachedContents.ToArray();
                cachedContents.Clear();
                var failure = ReleaseCachedContentsSynchronous(saved);
                if (failure != null)
                {
                    throw failure;
                }
            }
            finally
            {
                synchronousCacheClearing = false;
            }
        }

        /// <summary>使活动内容失去再次缓存资格，并同步销毁已有缓存。</summary>
        public void InvalidateCache()
        {
            RequireSynchronousNavigation();
            if (IsReentrant || IsSourceCommandRunning || HasCloseEvaluation || synchronousCacheClearing)
            {
                throw new InvalidOperationException("Cannot invalidate synchronous cache from navigation callbacks.");
            }

            cacheGeneration = new object();
            ClearCacheSynchronousCore();
        }

        private Exception ReleaseCachedContentsSynchronous(CachedContent[] saved)
        {
            var errors = new List<Exception>();
            retiringCachedViews += saved.Length;
            foreach (var entry in saved)
            {
                var before = errors.Count;
                try
                {
                    using (EnterCallback(null))
                    {
                        entry.Content.ReleaseCached(errors);
                    }
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                finally
                {
                    --retiringCachedViews;
                    var estimate = entry.Route.EstimatedRetainedBytes ?? 0;
                    if (before == errors.Count)
                    {
                        reservedCacheEstimatedBytes -= estimate;
                    }
                    else
                    {
                        failedCacheEstimatedBytes += estimate;
                    }
                }
            }

            if (errors.Count == 0)
            {
                return null;
            }

            var failure = new AggregateException("Synchronous navigation cache cleanup failed.", errors);
            if (cacheReleaseErrors.Count < terminalCapacity)
            {
                cacheReleaseErrors.Add(failure);
            }
            else if (omittedCacheReleaseErrors < long.MaxValue)
            {
                ++omittedCacheReleaseErrors;
            }

            return failure;
        }

        /// <summary>立即清理本导航器的预加载与停用页面，不启动任务或等待帧。</summary>
        public void ClearInactiveContent()
        {
            RequireSynchronousNavigation();
            if (!CanAwaitShutdown)
            {
                throw new InvalidOperationException("不能在导航回调中清理闲置内容。");
            }

            if (!IsShutdown)
            {
                if (synchronousInactiveClearing)
                {
                    throw new InvalidOperationException("UI 闲置内容正在清理。");
                }
                synchronousInactiveClearing = true;
                var errors = new List<Exception>();
                try
                {
                    try
                    {
                        ClearSynchronousPreloadsCore();
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                    try
                    {
                        ClearCache();
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
                finally
                {
                    synchronousInactiveClearing = false;
                }
                if (errors.Count != 0)
                {
                    throw new AggregateException("UI 闲置内容清理失败。", errors);
                }
            }
        }

        /// <summary>预检后停止接收请求，直接关闭页面、销毁停用页面缓存。</summary>
        private void ShutdownUntraced()
        {
            RequireSynchronousNavigation();
            if (!CanAwaitShutdown || pending != 0 || synchronousCacheClearing || synchronousShuttingDown)
            {
                throw new InvalidOperationException("Cannot shut down a synchronous navigator during navigation work.");
            }

            if (synchronousShutdownCompleted)
            {
                if (synchronousShutdownError != null)
                {
                    throw synchronousShutdownError;
                }
                return;
            }

            var saved = entries.Values.OrderByDescending(entry => entry.Order).ToArray();
            if (saved.Any(entry => !entry.CanReleaseSynchronously) ||
                cachedContents.Any(entry => !entry.Content.CanReleaseSynchronously))
            {
                throw new InvalidOperationException("Navigator resources cannot currently release synchronously.");
            }

            synchronousShuttingDown = true;
            var errors = new List<Exception>();
            void Attempt(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            // 停止状态先于外部回调发布，禁止销毁回调重新打开页面。
            Attempt(() => shutdown.Cancel(false));
            posted.Clear();
            synchronousCloseRequests.Clear();
            foreach (var entry in saved)
            {
                Attempt(() =>
                {
                    if (!entries.ContainsKey(entry.Handle))
                    {
                        return;
                    }
                    var result = BeginCloseSynchronous(entry, DismissReason.HostShutdown);
                    if (result.Error != null)
                    {
                        errors.Add(result.Error);
                    }
                    else if (result.Status == CloseStatus.Blocked)
                    {
                        errors.Add(new InvalidOperationException("View cleanup was blocked during synchronous shutdown."));
                    }
                });
            }

            try
            {
                ClearCacheSynchronousCore();
            }
            catch (Exception error)
            {
                if (!IsRecordedCacheFailure(error))
                {
                    errors.Add(error);
                }
            }

            try
            {
                ClearSynchronousPreloadsCore();
            }
            catch (Exception)
            {
                // 已记录的逐项失败在下方统一报告，避免重复计入。
            }
            errors.AddRange(preloadCleanupErrors);
            preloadCleanupErrors.Clear();
            errors.AddRange(cacheReleaseErrors);
            cacheReleaseErrors.Clear();
            if (omittedCacheReleaseErrors != 0)
            {
                errors.Add(new InvalidOperationException($"Additional navigation cache cleanup failures omitted: {omittedCacheReleaseErrors}."));
            }
            LifecycleChanged = null;
            lifecycleEvents.Clear();
            synchronousShutdownCompleted = true;
            synchronousShuttingDown = false;
            if (errors.Count != 0)
            {
                synchronousShutdownError = new AggregateException("Synchronous navigator shutdown failed.", errors);
                throw synchronousShutdownError;
            }
        }

        public void Dispose() => Shutdown();
    }
}
