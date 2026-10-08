using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    internal sealed partial class ViewInstance<TViewModel, TArgs, TResult>
        where TViewModel : ViewModel
    {
        private Exception argsUpdateCleanupFailure;

        TArgs IArgsUpdateHost<TArgs>.Args => Args;

        IArgsUpdatePresenter<TArgs> IArgsUpdateHost<TArgs>.ArgsUpdater => ArgsUpdater;

        IInputView IArgsUpdateHost<TArgs>.ArgsInput => view as IInputView;

        LifetimeScope IArgsUpdateHost<TArgs>.ArgsLifetime => activationLifetime;

        bool IArgsUpdateHost<TArgs>.CanUpdateArgs => IsActive && ActivationCommitted &&
                    !EnterPending && !HasCloseStarted && CloseRequest == null && !IsRebinding;

        internal ValueTask<ArgsUpdateOutcome> UpdateArgsAsync(TArgs args, CancellationToken token,
            Func<CancellationToken, ValueTask> beforeCommit = null, Action afterCommit = null)
        {
            var operation = new ArgsUpdateOperation<TArgs>(this, token, beforeCommit, afterCommit);
            ArgsUpdating = operation.Completion;
            CancelArgsUpdateCallback = operation.Cancel;
            return operation.Start(args);
        }

        void IArgsUpdateHost<TArgs>.RequireArgsThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("Argument update requires its creating UI thread.");
            }
        }

        void IArgsUpdateHost<TArgs>.SetArgs(TArgs args) => SetArgs(args);

        void IArgsUpdateHost<TArgs>.ArgsCommitted() => Owner.MarkInstanceUpdated(this);

        void IArgsUpdateHost<TArgs>.InvokeArgsCallback(Action callback) => InvokeLifecycle(callback);

        ValueTask IArgsUpdateHost<TArgs>.InvokeArgsCallbackAsync(Func<ValueTask> callback) => InvokeLifecycleAsync(callback);

        void IArgsUpdateHost<TArgs>.ArgsFaulted(Exception error)
        {
            SetFailure(error);
            Owner.CloseFailedArgsUpdate(this);
        }

        void IArgsUpdateHost<TArgs>.RecordArgsUpdateOutcome(ArgsUpdateOutcome outcome)
        {
            // 只保留首个清理失败，后续成功更新不能恢复该实例的缓存资格。
            if (outcome.Cleanup == ArgsUpdateCleanup.Failed && argsUpdateCleanupFailure == null)
            {
                argsUpdateCleanupFailure = outcome.Error;
            }
        }
    }
}
