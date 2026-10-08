using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs>
        where TViewModel : ViewModel
    {
        /// <summary>换绑及原模型释放仍在进行，关闭或停用会等待其排空。</summary>
        public bool IsRebinding => lifecycle != null && lifecycle.IsRebinding;

        /// <summary>当前是否具备换绑资格；实际执行仍会再次验证，不作为持有许可。</summary>
        public bool CanRebind
        {
            get
            {
                Owner.RequireThread();
                // 自身命令请求更换内容时应走候选替换，不能等待自己完成后再解绑。
                return !IsExecuting && !IsUpdatingArgs && !IsRebinding && CanUpdateArgs;
            }
        }

        /// <summary>
        /// 显式借用新模型，不重开界面或更换句柄。准备失败保留旧绑定，提交失败关闭子视图。
        /// 不能从当前子视图自己的命令或生命周期回调等待换绑。
        /// </summary>
        public ValueTask<RebindOutcome> RebindAsync(TViewModel next, CancellationToken cancellationToken = default)
        {
            using var diagnostic = Owner.BeginDiagnostic("ChildView.RebindAsync", "RebindPreparation");
            Owner.RequireThread();
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (IsLifecycleExecuting)
            {
                return RejectRebind(RebindRejection.Reentrant);
            }

            if (IsUpdatingArgs || IsRebinding)
            {
                return RejectRebind(RebindRejection.Busy);
            }

            if (!CanUpdateArgs)
            {
                return RejectRebind(RebindRejection.SourceUnavailable);
            }

            return ObserveRebindAsync(lifecycle.RebindAsync(next, view, this, template.BindingFactory,
                activation, RequireRebindActive, () => { }, RebindFaulted, cancellationToken));
        }

        /// <summary>父绑定预览使用；准备期间保留当前绑定，提交由父事务同步驱动。</summary>
        internal ValueTask<PreparedViewRebind> PrepareStagedRebindAsync(TViewModel next,
            CancellationToken cancellationToken)
        {
            using var diagnostic = Owner.BeginDiagnostic("ChildView.RebindAsync", "RebindPreparation");
            Owner.RequireThread();
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (!CanRebind || IsLifecycleExecuting)
            {
                throw new InvalidOperationException("Child view is unavailable for staged rebinding.");
            }

            var preparation = lifecycle.PrepareStagedRebindAsync(next, view, this, template.BindingFactory,
                activation, RequireRebindActive, () => { }, RebindFaulted, cancellationToken);
            if (lifecycle.IsRebinding)
            {
                _ = ObserveStagedRebindAsync(lifecycle.Rebinding);
            }

            return preparation;
        }

        private void RequireRebindActive()
        {
            Owner.RequireThread();
            activation.Token.ThrowIfCancellationRequested();
            if (!CanUpdateArgs)
            {
                throw new OperationCanceledException("Child view is unavailable for rebinding.");
            }
        }

        private void RebindFaulted(Exception error)
        {
            earlyErrors.Add(error);
            BeginCloseAndReport();
        }

        private async ValueTask<RebindOutcome> ObserveRebindAsync(ValueTask<RebindOutcome> operation)
        {
            var outcome = await operation;
            Owner.RefreshTicks();
            if (outcome.Error != null)
            {
                Invoke(() => UIErrors.Report(outcome.Error));
            }

            return outcome;
        }

        private async Task ObserveStagedRebindAsync(Task<RebindOutcome> operation)
        {
            try
            {
                await ObserveRebindAsync(new ValueTask<RebindOutcome>(operation));
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private static ValueTask<RebindOutcome> RejectRebind(RebindRejection reason) =>
            new ValueTask<RebindOutcome>(new RebindOutcome(RebindStatus.Rejected, reason));
    }
}
