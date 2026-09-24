using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    public sealed partial class PagedList<T, TCursor>
    {
        private ResetRequest pendingReset;
        private TaskCompletionSource<bool> resetDrain;

        /// <summary>每次接受重置立即换代，供界面拒绝旧查询的异步错误回写。</summary>
        public object QueryVersion { get; private set; } = new object();

        /// <summary>重置等待旧加载退出期间为真；保留旧条目，但不允许启动新页。</summary>
        public bool IsResetting => resetDrain != null;

        /// <summary>
        /// 取消并排空旧加载后清空条目、恢复首游标，可同时替换加载器以捕获新的查询条件。
        /// 快速重置只保留最新候选；省略加载器时沿用最近候选或现行加载器。
        /// 调用令牌仅取消等待，父生命周期取消整个来源；完成后不自动加载第一页。
        /// </summary>
        public ValueTask<PageResetResult> ResetAsync(TCursor initialCursor = default,
            Func<TCursor, int, CancellationToken, ValueTask<ListPage<T, TCursor>>> loader = null,
            CancellationToken cancellationToken = default)
        {
            RequireThread();
            RejectCallback();
            if (dispatchFailure != null)
            {
                return new ValueTask<PageResetResult>(new PageResetResult(PageResetStatus.Failed, dispatchFailure));
            }

            if (!IsActive)
            {
                return new ValueTask<PageResetResult>(new PageResetResult(PageResetStatus.Inactive));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<PageResetResult>(new PageResetResult(PageResetStatus.WaitCancelled));
            }

            var request = new ResetRequest
            {
                Cursor = initialCursor,
                Loader = loader ?? (pendingReset == null ? this.loader : pendingReset.Loader)
            };
            var previous = pendingReset;
            pendingReset = request;
            QueryVersion = new object();
            previous?.Completion.TrySetResult(new PageResetResult(PageResetStatus.Superseded));
            if (resetDrain == null)
            {
                var drain = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                resetDrain = drain;
                Error = null;
                PublishState();
                Exception cancellationError = null;
                try
                {
                    // 取消回调属于项目代码，期间禁止重新加载、重置和等待自身销毁。
                    InvokeOwnerAction(() =>
                    {
                        if (loadCancellation != null)
                        {
                            loadCancellation.Cancel();
                        }

                        return true;
                    });
                }
                catch (Exception error)
                {
                    // 回调失败不跳过排空，也不把失败的重置伪装成成功。
                    cancellationError = error;
                }

                _ = CompleteResetAsync(drain, loading == null ? null : loading.Task, cancellationError);
            }
            else
            {
                PublishState();
            }

            return new ValueTask<PageResetResult>(WaitForResetAsync(request.Completion.Task, cancellationToken));
        }

        private async Task CompleteResetAsync(TaskCompletionSource<bool> drain,
                    Task<PageLoadResult> previousLoad, Exception cancellationError)
        {
            try
            {
                if (previousLoad != null)
                {
                    await previousLoad;
                }

                await OnOwnerAsync(() =>
                {
                    var request = pendingReset;
                    PageResetResult result;
                    try
                    {
                        if (dispatchFailure != null)
                        {
                            result = new PageResetResult(PageResetStatus.Failed, dispatchFailure);
                        }
                        else if (!IsActive)
                        {
                            result = new PageResetResult(PageResetStatus.Inactive);
                        }
                        else if (cancellationError != null)
                        {
                            result = new PageResetResult(PageResetStatus.Failed, cancellationError);
                        }
                        else
                        {
                            ApplyReset(request);
                            result = new PageResetResult(PageResetStatus.Applied);
                        }
                    }
                    catch (Exception error)
                    {
                        result = new PageResetResult(PageResetStatus.Failed, error);
                    }

                    pendingReset = null;
                    resetDrain = null;
                    Error = result.Error;
                    PublishState();
                    request.Completion.TrySetResult(result);
                    return true;
                });
            }
            catch (Exception error)
            {
                Interlocked.CompareExchange(ref dispatchFailure, error, null);
                // 上下文损坏时只完成等待，不在线程池发布集合或界面通知。
                var request = Interlocked.Exchange(ref pendingReset, null);
                Interlocked.Exchange(ref resetDrain, null);
                request?.Completion.TrySetResult(new PageResetResult(PageResetStatus.Failed, error));
            }
            finally
            {
                drain.TrySetResult(true);
            }
        }

        private void ApplyReset(ResetRequest request)
        {
            var nextKeys = new HashSet<object>();
            var nextKeyOrder = new List<object>();
            var oldKeys = keys;
            var oldKeyOrder = keyOrder;
            var oldCursor = cursor;
            var oldLoader = loader;
            var oldHasMore = HasMore;
            keys = nextKeys;
            keyOrder = nextKeyOrder;
            cursor = request.Cursor;
            loader = request.Loader;
            HasMore = true;
            try
            {
                // 集合事务发布前同步切换查询配置，通知观察者不会读到混合状态。
                items.Clear();
            }
            catch
            {
                keys = oldKeys;
                keyOrder = oldKeyOrder;
                cursor = oldCursor;
                loader = oldLoader;
                HasMore = oldHasMore;
                throw;
            }
        }

        private static async Task<PageResetResult> WaitForResetAsync(Task<PageResetResult> shared, CancellationToken token)
        {
            if (!token.CanBeCanceled || shared.IsCompleted)
            {
                return await shared;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(shared, cancelled.Task) != shared)
                {
                    return new PageResetResult(PageResetStatus.WaitCancelled);
                }

                return await shared;
            }
        }

        private sealed class ResetRequest
        {
            internal TCursor Cursor;
            internal Func<TCursor, int, CancellationToken, ValueTask<ListPage<T, TCursor>>> Loader;
            internal readonly TaskCompletionSource<PageResetResult> Completion =
                            new TaskCompletionSource<PageResetResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
