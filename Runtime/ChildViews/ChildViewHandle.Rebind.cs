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
        /// 显式借用新模型，不重开界面或更换句柄。失败尝试恢复旧模型与绑定。
        /// 不能从当前子视图自己的命令或生命周期回调等待换绑。
        /// </summary>
        public ValueTask<RebindOutcome> RebindAsync(TViewModel next, CancellationToken cancellationToken = default)
        {
            Owner.RequireAsyncAllowed();
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
                activation, RequireRebindActive, () => { }, RebindRecoveryFailed, cancellationToken));
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

        private void RebindRecoveryFailed(Exception error)
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

        private static ValueTask<RebindOutcome> RejectRebind(RebindRejection reason) =>
            new ValueTask<RebindOutcome>(new RebindOutcome(RebindStatus.Rejected, reason));
    }
}
