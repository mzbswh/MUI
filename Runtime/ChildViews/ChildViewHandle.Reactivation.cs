using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs>
        where TViewModel : ViewModel
    {
        internal override async ValueTask PrepareReactivationCoreAsync(CancellationToken token)
        {
            Owner.RequireActive();
            if ((State != ChildViewState.Retained && State != ChildViewState.Inactive) || IsExecuting || Owner.HasVisualRetention)
            {
                throw new InvalidOperationException("Only an idle retained or inactive child view can prepare reactivation.");
            }

            token.ThrowIfCancellationRequested();
            var finished = BeginLifecycleTransition(ChildViewState.Preparing);
            try
            {
                RequireContentCurrent();
                using (new UIThreadCancellation(token, CancelWork))
                {
                    // 恢复前先结束旧画面，再完整释放旧激活；绝不并行复用仍被旧任务持有的实例。
                    view.SetHostState(false, false);
                    if (view is IVisualRetentionView retained)
                    {
                        Invoke(retained.EndVisualRetention);
                    }

                    var errors = new List<Exception>();
                    await EndActivationAsync(errors);
                    Owner.RequireThread();
                    if (errors.Count != 0)
                    {
                        throw new AggregateException("Previous childView activation cleanup failed.", errors);
                    }

                    RequireReactivationCurrent(token);
                    BeginFreshActivation();
                    using (var activeToken = CancellationTokenSource.CreateLinkedTokenSource(token, activation.Token))
                    {
                        Prepare();
                        RequireReactivationCurrent(activeToken.Token);
                        await PrepareAsync(activeToken.Token);
                        RequireReactivationCurrent(activeToken.Token);
                        FinishPreparation();
                    }
                }
            }
            catch (Exception error)
            {
                // 旧激活已经退役，失败只能清理当前实例，不能伪装恢复旧激活。
                RecordTransitionFailure(error);

                throw;
            }
            finally
            {
                // 此信号只表示恢复代码已不再访问实例，不承诺恢复成功。
                finished.TrySetResult(true);
            }
        }

        private void RequireReactivationCurrent(CancellationToken token)
        {
            RequireContentCurrent();
            Owner.RequireActive();
            token.ThrowIfCancellationRequested();
            if (State != ChildViewState.Preparing || closeStarted)
            {
                throw new OperationCanceledException("ChildView closed during reactivation.");
            }
        }
    }
}
