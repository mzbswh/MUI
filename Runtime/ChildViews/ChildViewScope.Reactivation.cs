using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewScope
    {
        /// <summary>
        /// 接受 Retained 或 Inactive 子项，必要时排空旧激活，再复用原 View、VM 和 Presenter 准备新激活。
        /// 成功只到 Prepared，调用者仍须 Commit；不重新申请 ViewLease，不再次调用 OnCreate。
        /// </summary>
        public ValueTask<ChildViewHandle> PrepareReactivationAsync(
            ChildViewHandle handle,
            CancellationToken cancellationToken = default)
        {
            ValidateTransition(handle, ChildViewState.Inactive, ChildViewState.Retained);

            cancellationToken.ThrowIfCancellationRequested();
            // 父级销毁等待此操作，恢复挂起期间不会提前归还同一份 ViewLease。
            return operations.RunAsync(token => ReactivateTrackedAsync(handle, token, cancellationToken));
        }

        private async ValueTask<ChildViewHandle> ReactivateTrackedAsync(
            ChildViewHandle handle,
            CancellationToken scopeToken,
            CancellationToken callerToken)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(scopeToken, owner.Token, callerToken))
            {
                try
                {
                    await handle.PrepareReactivationCoreAsync(linked.Token);
                    RequireActive();
                    linked.Token.ThrowIfCancellationRequested();
                    handle.SetParentState(visible, interactable);
                    RequireActive();
                    linked.Token.ThrowIfCancellationRequested();
                    if (handle.State != ChildViewState.Prepared)
                    {
                        throw new OperationCanceledException("ChildView closed before reactivation preparation returned.");
                    }

                    return handle;
                }
                catch (Exception failure)
                {
                    try
                    {
                        await handle.BeginClose();
                    }
                    catch (Exception cleanup)
                    {
                        throw new AggregateException("ChildView reactivation and cleanup failed.", failure, cleanup);
                    }

                    throw;
                }
            }
        }
    }
}
