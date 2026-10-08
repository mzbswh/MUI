using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        public ValueTask<CloseOutcome> BackAsync(CancellationToken cancellationToken = default) =>
            BackAsyncCore(cancellationToken, null);

        /// <summary>
        /// 宿主输入入口使用：视觉退出后允许下一次返回输入，关闭结果在提交时返回。
        /// 守卫拒绝或没有目标时，输入资格随本次请求完成恢复。
        /// </summary>
        public ValueTask<CloseOutcome> BackAsync(out Task inputSettled, CancellationToken cancellationToken = default)
        {
            AssertThread();
            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            inputSettled = settled.Task;
            return BackAsyncCore(cancellationToken, settled);
        }

        private ValueTask<CloseOutcome> BackAsyncCore(CancellationToken cancellationToken,
            TaskCompletionSource<bool> inputSettled)
        {
            AssertThread();
            var trace = BeginOperationTrace(string.Empty, "BackAsync");
            Action<ViewHandle> visualObserver = null;
            try
            {
                var outcome = FindBackTarget(out var target);
                trace = ResolveBackTraceTarget(trace, target);
                if (inputSettled != null && target != null)
                {
                    var handle = target.Handle;
                    visualObserver = completed =>
                    {
                        if (completed != handle)
                        {
                            return;
                        }

                        visualExitCompleted -= visualObserver;
                        inputSettled.TrySetResult(true);
                    };
                    visualExitCompleted += visualObserver;
                }

                using (EnterOperationTrace(trace))
                {
                    var closing = target == null ? Task.FromResult(outcome) : BeginRequestedClose(target, DismissReason.Back);
                    var operation = target == null ? new ValueTask<CloseOutcome>(outcome)
                        : new ValueTask<CloseOutcome>(WaitForCloseCommitAsync(target, closing, cancellationToken));
                    operation = trace.Id == 0 ? operation : ObserveTracedCloseAsync(operation, trace);
                    if (inputSettled == null)
                    {
                        return operation;
                    }

                    var completion = operation.AsTask();
                    // 提交成功不解除视觉屏障；守卫拒绝或清理终结时才使用后备信号。
                    _ = SettleBackInputAsync(closing, inputSettled, visualObserver);
                    return new ValueTask<CloseOutcome>(completion);
                }
            }
            catch (Exception error)
            {
                if (visualObserver != null)
                {
                    visualExitCompleted -= visualObserver;
                }

                inputSettled?.TrySetResult(true);
                FinishOperationTrace(trace, trace.Source, "抛出异常", error);
                throw;
            }
        }

        private async Task SettleBackInputAsync(Task<CloseOutcome> completion,
            TaskCompletionSource<bool> inputSettled, Action<ViewHandle> visualObserver)
        {
            try
            {
                await completion;
            }
            catch (Exception)
            {
                // 结果由原请求观察；此任务只负责恢复输入资格。
            }
            finally
            {
                if (visualObserver != null)
                {
                    visualExitCompleted -= visualObserver;
                }

                inputSettled.TrySetResult(true);
            }
        }

        private static NavigationTraceOperation ResolveBackTraceTarget(NavigationTraceOperation trace, ViewInstance target)
        {
            if (trace.Id == 0 || target == null)
            {
                return trace;
            }
            return trace.WithTarget(NavigationTraceEntry.Limit(target.Route.Key), target.Handle);
        }
    }
}
