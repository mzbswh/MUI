using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        private readonly TabCacheOptions cacheOptions;
        private readonly Dictionary<ChildViewHandle, TabContentDefinition> contentDefinitions =
                    new Dictionary<ChildViewHandle, TabContentDefinition>();
        private readonly LinkedList<CachedContent> cachedContents = new LinkedList<CachedContent>();
        private Task cacheMaintenance;
        private bool cacheInvalidationRequested;
        private readonly List<Exception> cacheCleanupErrors = new List<Exception>();

        /// <summary>已经排空旧激活并进入缓存的实例数，不包含当前内容与在途清理。</summary>
        public int CachedContentCount
        {
            get
            {
                scope.RequireThread();
                return cachedContents.Count;
            }
        }

        /// <summary>已缓存实例的合计估算字节；存在未知项或合计超出 long 范围时为 null。</summary>
        public long? CachedEstimatedBytes
        {
            get
            {
                scope.RequireThread();
                long total = 0;
                foreach (var saved in cachedContents)
                {
                    var size = saved.Definition.EstimatedRetainedBytes;
                    if (!size.HasValue || total > long.MaxValue - size.Value)
                    {
                        return null;
                    }

                    total += size.Value;
                }

                return total;
            }
        }

        private bool IsCacheCallback
        {
            get
            {
                foreach (var handle in contentDefinitions.Keys)
                {
                    if (handle.IsExecuting)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 从所属 UI 线程请求回收过期缓存；不等待释放，不改变当前选择。
        /// 无同步上下文的宿主应周期调用；自动扫描与手动调用共用同一条维护队列。
        /// </summary>
        public void RefreshCache()
        {
            scope.RequireThread();
            if (Mode == LifetimeMode.Synchronous)
            {
                RefreshCacheSynchronous();
                return;
            }

            if (inactive || !scope.IsActive ||
                cachedContents.First == null || !cacheMaintenance.IsCompleted)
            {
                return;
            }

            // 回调内只跳过本轮，避免在用户生命周期或状态发布期间嵌套释放其他实例。
            if (changing || publishing || cancellingLeaves || IsInGuard || IsRetainedCallback ||
                IsCacheCallback || slot.IsExecuting || (preparing.Value != null && preparing.Value.Active))
            {
                return;
            }

            // 资源失效不遵循缓存时间顺序，必须检查整个有界目录。
            foreach (var saved in new List<CachedContent>(cachedContents))
            {
                if (IsCacheInvalid(saved))
                {
                    ScheduleCacheInvalidation();
                    break;
                }
            }
        }

        private async ValueTask<bool> ScanCacheAsync(CancellationToken token)
        {
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, scope.Token))
            {
                try
                {
                    while (!inactive && scope.IsActive)
                    {
                        // 每个控制器最多一条扫描循环；维护较慢时等待完成，不累积定时请求。
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);
                        cancellation.Token.ThrowIfCancellationRequested();
                        scope.RequireThread();
                        RefreshCache();
                        await cacheMaintenance;
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // 父级关闭或控制器销毁结束扫描，资源释放仍由原维护与最终清理负责。
                }
            }

            return true;
        }

        private bool IsCacheInvalid(CachedContent saved)
        {
            if (!IsContentReusable(saved.Handle))
            {
                return true;
            }

            var duration = cacheOptions.TimeToLive;
            if (!duration.HasValue)
            {
                return false;
            }

            // 使用单调时钟，不受系统时间调整影响；从停用排空并实际入缓存时开始计时。
            var elapsedSeconds = (Stopwatch.GetTimestamp() - saved.CachedAt) / (double)Stopwatch.Frequency;
            return elapsedSeconds >= duration.Value.TotalSeconds;
        }

        private static bool IsContentReusable(ChildViewHandle handle)
        {
            try
            {
                return handle.IsContentCurrent;
            }
            catch (Exception error)
            {
                // 无法确认版本时拒绝复用；物理回收仍走现有维护和关闭协议。
                UIErrors.Report(error);
                return false;
            }
        }

        private bool HasCacheSizeEstimate(TabContentDefinition definition)
        {
            var budget = cacheOptions.MaxEstimatedBytes;
            return !budget.HasValue || (definition.EstimatedRetainedBytes.HasValue &&
                definition.EstimatedRetainedBytes.Value <= budget.Value);
        }

        private bool FitsCacheSizeBudget(TabContentDefinition definition)
        {
            var budget = cacheOptions.MaxEstimatedBytes;
            if (!budget.HasValue)
            {
                return true;
            }

            var total = CachedEstimatedBytes;
            // 准入已验证单项大小，用减法比较防止合计溢出。
            return total.HasValue && definition.EstimatedRetainedBytes.HasValue &&
                total.Value <= budget.Value - definition.EstimatedRetainedBytes.Value;
        }

        private void TrackContent(ChildViewHandle handle, TabContentDefinition definition)
        {
            if (cacheOptions.Capacity == 0)
            {
                return;
            }

            if (contentDefinitions.TryGetValue(handle, out var existing))
            {
                if (!ReferenceEquals(existing, definition))
                {
                    throw new InvalidOperationException("An existing Tab instance cannot change its content definition.");
                }

                return;
            }

            contentDefinitions.Add(handle, definition);
            handle.Closed += OnTrackedContentClosed;
        }

        private void OnTrackedContentClosed(ChildViewHandle handle)
        {
            contentDefinitions.Remove(handle);
            for (var node = cachedContents.First; node != null; node = node.Next)
            {
                if (ReferenceEquals(node.Value.Handle, handle))
                {
                    cachedContents.Remove(node);
                    break;
                }
            }
        }

        private bool IsCacheDefinitionCurrent(TabContentDefinition definition)
        {
            if (inactive || !scope.IsActive || !definitions.TryGetValue(definition.Key, out var current) ||
                !ReferenceEquals(current, definition))
            {
                return false;
            }

            foreach (var item in ViewModel.Items)
            {
                if (item.Key == definition.Key)
                {
                    return item.Enabled;
                }
            }

            return false;
        }

        private async ValueTask<bool> CacheRetiredAsync(ChildViewHandle handle)
        {
            await cacheMaintenance;
            if (!contentDefinitions.TryGetValue(handle, out var definition) ||
                !cacheOptions.Allows(definition.Key) || !HasCacheSizeEstimate(definition) || !IsContentReusable(handle) || !IsCacheDefinitionCurrent(definition) ||
                (displayedDefinition != null && displayedDefinition.Key == definition.Key))
            {
                return false;
            }

            // 槽等待此操作结束后才启动下个候选，未排空实例始终占据唯一退役操作。
            await scope.DeactivateAsync(handle);
            if (!IsContentReusable(handle) || !IsCacheDefinitionCurrent(definition))
            {
                return false;
            }

            while (true)
            {
                // 淘汰期间可能有新的目录清理；全部收敛后才能占用腾出的容量。
                await cacheMaintenance;
                if (!IsContentReusable(handle) || !IsCacheDefinitionCurrent(definition))
                {
                    return false;
                }

                if (cachedContents.Count < cacheOptions.Capacity && FitsCacheSizeBudget(definition))
                {
                    break;
                }

                await EvictCachedAsync(cachedContents.First.Value);
            }

            if (!IsContentReusable(handle) || !IsCacheDefinitionCurrent(definition))
            {
                return false;
            }

            cachedContents.AddLast(new CachedContent
            {
                Handle = handle,
                Definition = definition,
                CachedAt = Stopwatch.GetTimestamp()
            });
            return true;
        }

        private async ValueTask<ChildViewHandle> TakeCachedAsync(TabContentDefinition definition,
                    bool forceReload, CancellationToken token)
        {
            await cacheMaintenance;
            token.ThrowIfCancellationRequested();
            for (var node = cachedContents.First; node != null; node = node.Next)
            {
                var saved = node.Value;
                if (saved.Definition.Key != definition.Key)
                {
                    continue;
                }

                cachedContents.Remove(node);
                if (forceReload || IsCacheInvalid(saved) || !ReferenceEquals(saved.Definition, definition) ||
                    saved.Handle.State != ChildViewState.Inactive)
                {
                    await saved.Handle.BeginClose();
                    token.ThrowIfCancellationRequested();
                    return null;
                }

                try
                {
                    // 移出缓存后由本次准备接管；成功返回的候选再交给原 Slot 提交。
                    return await scope.PrepareReactivationAsync(saved.Handle, token);
                }
                catch (Exception failure)
                {
                    // Scope 的入口校验或操作登记前取消也可能失败，此时其内部回滚尚未接管。
                    // 明确关闭移出的实例，不能只保留在父 Scope 中直到父界面销毁。
                    try
                    {
                        await saved.Handle.BeginClose();
                    }
                    catch (Exception cleanup)
                    {
                        if (!ReferenceEquals(failure, cleanup))
                        {
                            throw new AggregateException("Cached Tab reactivation and cleanup failed.", failure, cleanup);
                        }
                    }

                    throw;
                }
            }

            return null;
        }

        private async Task EvictCachedAsync(CachedContent saved)
        {
            cachedContents.Remove(saved);
            await saved.Handle.BeginClose();
        }

        private void ScheduleCacheInvalidation()
        {
            if (cacheOptions.Capacity == 0 || inactive || !scope.IsActive)
            {
                return;
            }

            cacheInvalidationRequested = true;
            if (!cacheMaintenance.IsCompleted)
            {
                return;
            }

            // 后续目录变化只合并为一次复查；不为每次变化累加等待任务。
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            cacheMaintenance = completion.Task;
            Observe(work.RunAsync(_ => InvalidateCacheAsync(completion)).AsTask());
        }

        private async ValueTask<bool> InvalidateCacheAsync(TaskCompletionSource<bool> completion)
        {
            try
            {
                while (cacheInvalidationRequested)
                {
                    cacheInvalidationRequested = false;
                    var clearAll = cacheClearRequested;
                    cacheClearRequested = false;
                    foreach (var saved in new List<CachedContent>(cachedContents))
                    {
                        if (clearAll || IsCacheInvalid(saved) || !IsCacheDefinitionCurrent(saved.Definition))
                        {
                            try
                            {
                                await EvictCachedAsync(saved);
                            }
                            catch (Exception error)
                            {
                                cacheCleanupErrors.Add(error);
                                RecordCacheClearFailure(error);
                                UIErrors.Report(error);
                            }
                        }
                    }

                    if (clearAll)
                    {
                        CompleteCacheClear();
                    }
                }
            }
            catch (Exception error)
            {
                cacheCleanupErrors.Add(error);
                RecordCacheClearFailure(error);
                if (cacheClearCompletion != null && !cacheClearCompletion.Task.IsCompleted)
                {
                    cacheClearRequested = false;
                    CompleteCacheClear();
                }

                throw;
            }
            finally
            {
                completion.TrySetResult(true);
            }

            return true;
        }

        private async Task DisposeCacheAsync(List<Exception> errors)
        {
            await cacheMaintenance;
            foreach (var saved in new List<CachedContent>(cachedContents))
            {
                try
                {
                    await EvictCachedAsync(saved);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            foreach (var handle in contentDefinitions.Keys)
            {
                handle.Closed -= OnTrackedContentClosed;
            }

            contentDefinitions.Clear();
            errors.AddRange(cacheCleanupErrors);
            cacheCleanupErrors.Clear();
        }

        private sealed class CachedContent
        {
            public ChildViewHandle Handle;
            public TabContentDefinition Definition;
            public long CachedAt;
        }
    }
}
