using System;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs> where TViewModel : ViewModel
    {
        private bool synchronousUpdatingArgs;

        /// <summary>同步准备、提交或回滚参数候选并释放候选资源，不重开视图或换绑模型。</summary>
        public ArgsUpdateOutcome UpdateArgs(TArgs nextArgs)
        {
            Owner.RequireThread();
            if (Owner.Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("UpdateArgs requires a synchronous child scope.");
            }

            if (IsLifecycleExecuting)
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Reentrant);
            }

            if (IsUpdatingArgs || IsRebinding)
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Busy);
            }

            if (!CanUpdateArgs)
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.SourceUnavailable);
            }

            if (!(presenter is ISynchronousArgsUpdatePresenter<TArgs> updater))
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Unsupported);
            }

            if (!(view is IInputView))
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.InputControlUnsupported);
            }

            synchronousUpdatingArgs = true;
            try
            {
                return Owner.RunSynchronous(() =>
                {
                    var outcome = ArgsUpdateOperation<TArgs>.RunSynchronous(this, updater, nextArgs,
                        () => synchronousUpdatingArgs = false);
                    if (outcome.Error != null)
                    {
                        Invoke(() => UIErrors.Report(outcome.Error));
                    }

                    return outcome;
                });
            }
            finally
            {
                synchronousUpdatingArgs = false;
            }
        }
    }
}
