using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    internal sealed partial class ViewInstance<TViewModel, TArgs, TResult>
        where TViewModel : ViewModel
    {
        internal override Task Rebinding => lifecycle == null ? null : lifecycle.Rebinding;

        internal override bool IsRebinding => lifecycle != null && lifecycle.IsRebinding;

        internal override int ExecutingBindingCommandCount => binding == null ? 0 : binding.ExecutingCommandCount;

        internal override bool IsExecutingBindingCommand => binding != null && binding.IsExecutingCommand;

        internal override void CancelRebind()
        {
            if (lifecycle != null)
            {
                lifecycle.CancelRebind();
            }
        }

        internal ValueTask<RebindOutcome> RebindAsync(TViewModel next, CancellationToken token,
            Func<CancellationToken, ValueTask> enterCommit, Action exitCommit) =>
                    lifecycle.RebindAsync(next, view, this, route.BindingFactory, activationLifetime,
                        RequireRebindActive, () => Owner.MarkInstanceUpdated(this),
                        RebindFaulted, token, enterCommit, exitCommit);

        private void RequireRebindActive()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("View rebinding requires its creating UI thread.");
            }

            ActivationToken.ThrowIfCancellationRequested();
            if (!IsActive || !ActivationCommitted || EnterPending || HasCloseStarted ||
                content == null || !ReferenceEquals(content.BindingGeneration, BindingRegistry.Generation) ||
                !Owner.IsProviderVersionCurrent(content))
            {
                throw new OperationCanceledException("View is unavailable for rebinding.");
            }

            Owner.RequireRebindOwnershipCurrent(this);

            // 版本读取可执行外部代码，回调后再次检查激活资格。
            ActivationToken.ThrowIfCancellationRequested();
            if (!IsActive || HasCloseStarted || content == null ||
                !ReferenceEquals(content.BindingGeneration, BindingRegistry.Generation))
            {
                throw new OperationCanceledException("View closed or bindings changed while checking rebind eligibility.");
            }
        }

        private void RebindFaulted(Exception error)
        {
            SetFailure(error);
            Owner.CloseFailedRebind(this);
        }
    }
}
