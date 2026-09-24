using System;

namespace MUI.Navigation
{
    internal sealed partial class ViewInstance<TViewModel, TArgs, TResult> where TViewModel : ViewModel
    {
        private bool synchronousUpdatingArgs;

        internal override bool IsUpdatingArgs => synchronousUpdatingArgs || base.IsUpdatingArgs;

        internal ISynchronousArgsUpdatePresenter<TArgs> SynchronousArgsUpdater =>
                    presenter as ISynchronousArgsUpdatePresenter<TArgs>;

        /// <summary>复用父子视图共用的同步事务，候选释放后才撤销忙碌标志。</summary>
        internal ArgsUpdateOutcome UpdateArgs(TArgs args, Action beforeCommit = null)
        {
            if (Mode != LifetimeMode.Synchronous || IsUpdatingArgs || IsRebinding)
            {
                throw new InvalidOperationException("View is not ready for a synchronous argument update.");
            }

            synchronousUpdatingArgs = true;
            try
            {
                return ArgsUpdateOperation<TArgs>.RunSynchronous(this, SynchronousArgsUpdater, args,
                    () => synchronousUpdatingArgs = false, beforeCommit);
            }
            finally
            {
                synchronousUpdatingArgs = false;
            }
        }

        /// <summary>沿用原实例、参数和句柄；新模型借用，原自有模型由共用生命周期释放。</summary>
        internal RebindOutcome Rebind(TViewModel next) =>
            lifecycle.Rebind(next, view, this, route.BindingFactory, activationLifetime,
                RequireRebindActive, () => Owner.MarkInstanceUpdated(this), RebindRecoveryFailed);
    }
}
