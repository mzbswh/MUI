using System;
using System.Collections.Generic;
using System.Diagnostics;
using MUI.ChildViews;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        private bool CacheRetiredSynchronous(ChildViewHandle handle)
        {
            if (!contentDefinitions.TryGetValue(handle, out var definition) ||
                !cacheOptions.Allows(definition.Key) || !HasCacheSizeEstimate(definition) ||
                !IsContentReusable(handle) || !IsCacheDefinitionCurrent(definition) ||
                (displayedDefinition != null && displayedDefinition.Key == definition.Key))
            {
                return false;
            }

            scope.Deactivate(handle);
            if (!IsContentReusable(handle) || !IsCacheDefinitionCurrent(definition))
            {
                return false;
            }

            while (cachedContents.Count >= cacheOptions.Capacity || !FitsCacheSizeBudget(definition))
            {
                EvictCachedSynchronous(cachedContents.First.Value);
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

        private ChildViewHandle TakeCachedSynchronous(TabContentDefinition definition, bool forceReload)
        {
            for (var node = cachedContents.First; node != null; node = node.Next)
            {
                var saved = node.Value;
                if (saved.Definition.Key != definition.Key)
                {
                    continue;
                }

                if (forceReload || IsCacheInvalid(saved) || !ReferenceEquals(saved.Definition, definition) ||
                    saved.Handle.State != ChildViewState.Inactive)
                {
                    EvictCachedSynchronous(saved);
                    return null;
                }

                cachedContents.Remove(node);
                try
                {
                    return scope.PrepareReactivation(saved.Handle);
                }
                catch (Exception failure)
                {
                    try
                    {
                        saved.Handle.Dispose();
                    }
                    catch (Exception cleanup)
                    {
                        cacheCleanupErrors.Add(cleanup);
                        throw ChildViewPreparationException.SynchronousCleanupFailed(failure, cleanup);
                    }

                    throw;
                }
            }

            return null;
        }

        private void EvictCachedSynchronous(CachedContent saved)
        {
            cachedContents.Remove(saved);
            try
            {
                saved.Handle.Dispose();
            }
            catch (Exception error)
            {
                cacheCleanupErrors.Add(error);
                throw;
            }
        }

        /// <summary>同步检查整个有界目录并逐项回收；一项失败不阻止其他项释放。</summary>
        private void InvalidateCacheSynchronous(bool clearAll)
        {
            var errors = new List<Exception>();
            foreach (var saved in new List<CachedContent>(cachedContents))
            {
                if (clearAll || IsCacheInvalid(saved) || !IsCacheDefinitionCurrent(saved.Definition))
                {
                    try
                    {
                        EvictCachedSynchronous(saved);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("Synchronous Tab cache clearing failed.", errors);
            }
        }

        /// <summary>同步释放全部停用缓存；当前已显示的内容不受影响。</summary>
        public void ClearCache()
        {
            RequireSynchronousMode();
            scope.RequireActive();
            if (inactive || IsChanging)
            {
                throw new InvalidOperationException("Cannot clear the synchronous Tab cache during callbacks or after disposal.");
            }

            changing = true;
            try
            {
                scope.RunSynchronous(() => work.Run(_ =>
                {
                    InvalidateCacheSynchronous(true);
                    return true;
                }));
            }
            finally
            {
                changing = false;
            }
        }

        private void RefreshCacheSynchronous()
        {
            // 无闲置内容时不进入维护事务，避免逐帧创建委托和空缓存快照。
            if (cachedContents.Count == 0 || inactive || !scope.IsActive || IsChanging)
            {
                return;
            }

            changing = true;
            try
            {
                scope.RunSynchronous(() => work.Run(_ =>
                {
                    InvalidateCacheSynchronous(false);
                    return true;
                }));
            }
            finally
            {
                changing = false;
            }
        }

        private void RefreshAvailabilitySynchronous()
        {
            if (inactive || !scope.IsActive || IsChanging)
            {
                return;
            }

            changing = true;
            try
            {
                scope.RunSynchronous(() => work.Run(_ =>
                {
                    foreach (var definition in definitions.Values)
                    {
                        ViewModel.SetEnabled(definition.Key, definition.IsEnabled());
                    }

                    InvalidateCacheSynchronous(false);
                    return true;
                }));
            }
            finally
            {
                changing = false;
            }
        }
    }
}
