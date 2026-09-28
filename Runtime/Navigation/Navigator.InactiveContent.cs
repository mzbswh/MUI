using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private Task inactiveContentClearing;
        private bool synchronousInactiveClearing;

        /// <summary>正在取消预加载并释放非活动缓存；期间不接纳新预加载或关闭缓存。</summary>
        public bool IsClearingInactiveContent
        {
            get
            {
                AssertThread();
                return synchronousInactiveClearing || (Mode == LifetimeMode.AsyncAllowed &&
                    inactiveContentClearing != null && !inactiveContentClearing.IsCompleted);
            }
        }

        /// <summary>
        /// 先请求结束预加载，再释放停用页面缓存，等待全部相关回收。
        /// 活动页面、业务任务与导航历史保持有效；重复请求共用当前回收任务。
        /// </summary>
        private ValueTask ClearInactiveContentAsyncUntraced()
        {
            AssertThread();
            RequireAsyncNavigation();
            if (IsReentrant || IsSourceCommandRunning || HasCloseEvaluation)
            {
                throw new InvalidOperationException("不能在导航回调中等待闲置内容清理。");
            }

            if (IsShutdown)
            {
                return ShutdownAsyncUntraced();
            }

            if (inactiveContentClearing != null && !inactiveContentClearing.IsCompleted)
            {
                return new ValueTask(inactiveContentClearing);
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            inactiveContentClearing = completion.Task;
            _ = ClearInactiveContentCoreAsync(completion);
            return new ValueTask(inactiveContentClearing);
        }

        private async Task ClearInactiveContentCoreAsync(TaskCompletionSource<bool> completion)
        {
            try
            {
                var errors = new List<Exception>();
                // 两类 UI 内容独立发起清理，一类失败不跳过另一类。
                Task preloads = null;
                Task cache = null;
                try
                {
                    preloads = ClearInactivePreloadsAsync();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                try
                {
                    cache = BeginCacheClear();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                if (preloads != null)
                {
                    try
                    {
                        await preloads;
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
                if (cache != null)
                {
                    try
                    {
                        await cache;
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
                if (errors.Count != 0)
                {
                    throw new AggregateException("UI 闲置内容清理失败。", errors);
                }
                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }

        private async Task ClearInactivePreloadsAsync()
        {
            var errors = new List<Exception>();
            var pending = new HashSet<Task>();
            try
            {
                pending.Add(StartPreloadClear());
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            // 包含此前已经移出目录的预加载批次，等待其持有权归还。
            pending.UnionWith(preloadClearings);
            foreach (var cleanup in pending)
            {
                try
                {
                    await cleanup;
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("UI 预加载清理失败。", errors);
            }
        }

        private static bool ContainsCleanupError(Exception container, Exception target) =>
            ReferenceEquals(container, target) || (container is AggregateException aggregate &&
                System.Linq.Enumerable.Any(aggregate.InnerExceptions, nested => ContainsCleanupError(nested, target)));

        private bool IsRecordedInactiveCleanup(Exception error)
        {
            if (cacheReleaseErrors.Contains(error) || preloadCleanupErrors.Contains(error))
            {
                return true;
            }

            return error is AggregateException aggregate && aggregate.InnerExceptions.Count != 0 &&
                System.Linq.Enumerable.All(aggregate.InnerExceptions, IsRecordedInactiveCleanup);
        }
    }
}
