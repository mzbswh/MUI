using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs>
        where TViewModel : ViewModel
    {
        internal override async ValueTask DeactivateCoreAsync(CancellationToken token)
        {
            Owner.RequireActive();
            if ((State != ChildViewState.Active && State != ChildViewState.Retained) ||
                !bindingsCommitted || committing || IsExecuting || Owner.HasVisualRetention)
            {
                throw new InvalidOperationException("Only a stable active or retained child view can deactivate.");
            }

            token.ThrowIfCancellationRequested();
            var finished = BeginLifecycleTransition(ChildViewState.Deactivating);
            try
            {
                // 先失去业务资格，再执行可能重入的门控、取消与生命周期回调。
                Owner.RefreshTicks();
                StopForParentClose();
                if (view is IVisualRetentionView retained)
                {
                    Invoke(retained.EndVisualRetention);
                }

                var errors = new List<Exception>();
                await EndActivationAsync(errors);
                Owner.RequireThread();
                if (errors.Count != 0)
                {
                    throw new AggregateException("Child view deactivation cleanup failed.", errors);
                }

                Owner.RequireActive();
                token.ThrowIfCancellationRequested();
                if (State != ChildViewState.Deactivating || closeStarted)
                {
                    throw new OperationCanceledException("Child view closed during deactivation.");
                }

                // 只有旧绑定、任务和子激活全部排空后，才允许进入可复用状态。
                State = ChildViewState.Inactive;
            }
            catch (Exception error)
            {
                RecordTransitionFailure(error);

                throw;
            }
            finally
            {
                // 外部关闭等待此信号后才能执行最终销毁，避免重复并发清理旧激活。
                finished.TrySetResult(true);
            }
        }
    }
}
