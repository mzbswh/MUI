using System;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs> where TViewModel : ViewModel
    {
        /// <summary>同步借用新模型并换绑原视图；失败恢复旧绑定，无法恢复时关闭同一实例。</summary>
        public RebindOutcome Rebind(TViewModel next)
        {
            Owner.RequireThread();
            if (Owner.Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("Rebind requires a synchronous child scope.");
            }

            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (IsLifecycleExecuting)
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.Reentrant);
            }

            if (IsUpdatingArgs || IsRebinding)
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.Busy);
            }

            if (!CanUpdateArgs)
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.SourceUnavailable);
            }

            return Owner.RunSynchronous(() =>
            {
                var outcome = lifecycle.Rebind(next, view, this, template.BindingFactory, activation,
                    RequireRebindActive, () => { }, RebindRecoveryFailed);
                Owner.RefreshTicks();
                if (outcome.Error != null)
                {
                    Invoke(() => UIErrors.Report(outcome.Error));
                }

                return outcome;
            });
        }
    }
}
