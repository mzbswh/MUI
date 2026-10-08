using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        private TaskCompletionSource<bool> cacheClearCompletion;
        private bool cacheClearRequested;
        private readonly List<Exception> cacheClearErrors = new List<Exception>();

        /// <summary>
        /// 释放已缓存的停用实例并等待物理清理，不关闭当前内容或取消新选择。
        /// 重复调用共享同次清理；取消令牌只结束调用者等待，不撤销已经接受的清理。
        /// </summary>
        public ValueTask ClearCacheAsync(CancellationToken cancellationToken = default)
        {
            scope.RequireThread();
            scope.RequireActive();
            if (inactive)
            {
                throw new ObjectDisposedException(nameof(TabContentController));
            }

            if (changing || publishing || cancellingLeaves || IsInGuard || IsRetainedCallback ||
                IsCacheCallback || slot.IsExecuting || (preparing.Value != null && preparing.Value.Active))
            {
                throw new InvalidOperationException("Cannot await cache clearing from a Tab content or controller callback.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (cacheOptions.Capacity == 0)
            {
                return default;
            }

            if (cacheClearCompletion == null || cacheClearCompletion.Task.IsCompleted)
            {
                cacheClearCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                cacheClearErrors.Clear();
                cacheClearRequested = true;
                ScheduleCacheInvalidation();
            }

            return new ValueTask(WaitCacheClearAsync(cacheClearCompletion.Task, cancellationToken));
        }

        private void RecordCacheClearFailure(Exception error)
        {
            if (cacheClearCompletion != null && !cacheClearCompletion.Task.IsCompleted)
            {
                cacheClearErrors.Add(error);
            }
        }

        private void CompleteCacheClear()
        {
            if (cacheClearErrors.Count == 0)
            {
                cacheClearCompletion.TrySetResult(true);
            }
            else
            {
                cacheClearCompletion.TrySetException(new AggregateException("Tab cache clearing failed.", cacheClearErrors));
            }

            cacheClearErrors.Clear();
        }

        private static async Task WaitCacheClearAsync(Task shared, CancellationToken token)
        {
            if (!token.CanBeCanceled || shared.IsCompleted)
            {
                await shared;
                return;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(shared, cancelled.Task) != shared)
                {
                    // 后台取消仅影响等待，不在回调线程修改缓存或触碰 Unity 对象。
                    _ = ObserveAbandonedCacheClearAsync(shared);
                    throw new OperationCanceledException(token);
                }

                await shared;
            }
        }

        private static async Task ObserveAbandonedCacheClearAsync(Task shared)
        {
            try
            {
                await shared;
            }
            catch (Exception)
            {
                // 每个释放异常已由清理队列报告并登记到控制器最终结果；这里只观察无人等待的聚合任务。
            }
        }
    }
}
