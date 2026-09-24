using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs>
        where TViewModel : ViewModel
    {
        private ArgsUpdateOperation<TArgs> argsUpdate;
        private Exception argsUpdateCleanupFailure;

        public TArgs Args => args;

        public bool IsUpdatingArgs => synchronousUpdatingArgs || (argsUpdate != null && !argsUpdate.Completion.IsCompleted);

        private bool IsArgsHostActive => IsActive && bindingsCommitted && !committing && !closeStarted &&
                    !Owner.HasVisualRetention;

        // 资源版本读取属于外部接口，回调后再次检查父级与实例资格。
        private bool CanUpdateArgs => IsArgsHostActive && IsContentCurrent && IsArgsHostActive;

        TArgs IArgsUpdateHost<TArgs>.Args => args;

        IArgsUpdatePresenter<TArgs> IArgsUpdateHost<TArgs>.ArgsUpdater => presenter as IArgsUpdatePresenter<TArgs>;

        IInputView IArgsUpdateHost<TArgs>.ArgsInput => view as IInputView;

        CancellationToken IArgsUpdateHost<TArgs>.ArgsLifetimeToken => activation.Token;

        bool IArgsUpdateHost<TArgs>.CanUpdateArgs => CanUpdateArgs;

        /// <summary>
        /// 更新活动子视图参数，共用顶层页面的候选协议，不重开或换绑。
        /// 同一子项仅接受一个更新；停用、关闭与父取消会撤销资格并等待候选收尾。
        /// </summary>
        public ValueTask<ArgsUpdateOutcome> UpdateArgsAsync(TArgs nextArgs, CancellationToken cancellationToken = default)
        {
            Owner.RequireAsyncAllowed();
            if (IsLifecycleExecuting)
            {
                return RejectArgsUpdate(ArgsUpdateRejection.Reentrant);
            }

            if (IsUpdatingArgs || IsRebinding)
            {
                return RejectArgsUpdate(ArgsUpdateRejection.Busy);
            }

            if (!CanUpdateArgs)
            {
                return RejectArgsUpdate(ArgsUpdateRejection.SourceUnavailable);
            }

            if (!(presenter is IArgsUpdatePresenter<TArgs>))
            {
                return RejectArgsUpdate(ArgsUpdateRejection.Unsupported);
            }

            if (!(view is IInputView))
            {
                return RejectArgsUpdate(ArgsUpdateRejection.InputControlUnsupported);
            }

            argsUpdate = new ArgsUpdateOperation<TArgs>(this, cancellationToken);
            return ObserveArgsUpdateAsync(argsUpdate.Start(nextArgs));
        }

        private async ValueTask<ArgsUpdateOutcome> ObserveArgsUpdateAsync(ValueTask<ArgsUpdateOutcome> update)
        {
            var outcome = await update;
            if (outcome.Error != null)
            {
                Invoke(() => UIErrors.Report(outcome.Error));
            }

            return outcome;
        }

        void IArgsUpdateHost<TArgs>.RequireArgsThread() => Owner.RequireThread();

        void IArgsUpdateHost<TArgs>.SetArgs(TArgs nextArgs)
        {
            presenter.SetArgs(nextArgs);
            args = nextArgs;
        }

        void IArgsUpdateHost<TArgs>.ArgsCommitted()
        {
        }

        void IArgsUpdateHost<TArgs>.InvokeArgsCallback(Action callback) => Invoke(callback);

        ValueTask IArgsUpdateHost<TArgs>.InvokeArgsCallbackAsync(Func<ValueTask> callback) => InvokeAsync(callback);

        void IArgsUpdateHost<TArgs>.ArgsRecoveryFailed(Exception error)
        {
            earlyErrors.Add(error);
            BeginCloseAndReport();
        }

        void IArgsUpdateHost<TArgs>.RecordArgsUpdateOutcome(ArgsUpdateOutcome outcome)
        {
            // 有界保留首个历史清理失败，不能让后续成功更新把异常子项送回停用缓存。
            if (outcome.Cleanup == ArgsUpdateCleanup.Failed && argsUpdateCleanupFailure == null)
            {
                argsUpdateCleanupFailure = outcome.Error;
            }
        }

        private static ValueTask<ArgsUpdateOutcome> RejectArgsUpdate(ArgsUpdateRejection rejection) =>
                    new ValueTask<ArgsUpdateOutcome>(new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, rejection));
    }
}
