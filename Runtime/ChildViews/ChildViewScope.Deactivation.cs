using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewScope
    {
        /// <summary>
        /// 停止并隐藏同一 Scope 的稳定子视图，等待旧激活完整清理，保留实例资源。
        /// 成功后为 Inactive；准备新激活使用 PrepareReactivationAsync。失败或取消关闭原实例。
        /// </summary>
        public ValueTask DeactivateAsync(ChildViewHandle handle, CancellationToken cancellationToken = default)
        {
            ValidateTransition(handle, ChildViewState.Active, ChildViewState.Retained);

            cancellationToken.ThrowIfCancellationRequested();
            // 将在途停用纳入父级排空，父级不能在关闭回调尚未结束时归还资源。
            return new ValueTask(operations.RunAsync(token => DeactivateTrackedAsync(handle, token, cancellationToken)).AsTask());
        }

        private async ValueTask<bool> DeactivateTrackedAsync(
            ChildViewHandle handle,
            CancellationToken scopeToken,
            CancellationToken callerToken)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(scopeToken, owner.Token, callerToken))
            {
                try
                {
                    await handle.DeactivateCoreAsync(linked.Token);
                    RequireActive();
                    linked.Token.ThrowIfCancellationRequested();
                    if (handle.State != ChildViewState.Inactive)
                    {
                        throw new OperationCanceledException("Child view closed before deactivation returned.");
                    }

                    return true;
                }
                catch (Exception failure)
                {
                    try
                    {
                        await handle.BeginClose();
                    }
                    catch (Exception cleanup)
                    {
                        throw new AggregateException("Child view deactivation and final cleanup failed.", failure, cleanup);
                    }

                    throw;
                }
            }
        }
    }
}
