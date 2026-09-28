using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>当前实例是否仍有显式打开关系；终态或未知句柄返回 false。</summary>
        public bool HasExplicitOwnership(ViewHandle handle)
        {
            AssertThread();
            return entries.ContainsKey(handle) && ownership.HasExplicitOwner(handle);
        }

        /// <summary>
        /// 同步撤销显式持有。仍有父拥有者时保留实例并撤销独立历史和焦点资格；
        /// 否则经过同步关闭守卫，拒绝时保留显式关系。
        /// </summary>
        public ExplicitOwnershipReleaseOutcome ReleaseExplicitOwnership(ViewHandle handle)
        {
            AssertThread();
            var preflight = CheckExplicitRelease(handle, out var instance);
            if (preflight.HasValue)
            {
                return preflight.Value;
            }
            if (ownership.HasOwners(handle))
            {
                return ReleaseRetainedExplicitOwnership(instance);
            }
            return CompleteExplicitRelease(handle, Close(handle));
        }

        /// <summary>异步关闭只在末拥有者撤销时发生；取消只结束本次等待，不撤回已提交的关闭。</summary>
        public ValueTask<ExplicitOwnershipReleaseOutcome> ReleaseExplicitOwnershipAsync(ViewHandle handle,
            CancellationToken cancellationToken = default)
        {
            AssertThread();
            RequireAsyncNavigation();
            cancellationToken.ThrowIfCancellationRequested();
            var preflight = CheckExplicitRelease(handle, out var instance);
            if (preflight.HasValue)
            {
                return new ValueTask<ExplicitOwnershipReleaseOutcome>(preflight.Value);
            }
            if (ownership.HasOwners(handle))
            {
                return new ValueTask<ExplicitOwnershipReleaseOutcome>(ReleaseRetainedExplicitOwnership(instance));
            }
            return ReleaseLastExplicitOwnershipAsync(handle, cancellationToken);
        }

        private async ValueTask<ExplicitOwnershipReleaseOutcome> ReleaseLastExplicitOwnershipAsync(
            ViewHandle handle, CancellationToken token)
        {
            var closed = await CloseAsync(handle, token);
            AssertThread();
            return CompleteExplicitRelease(handle, closed);
        }

        private ExplicitOwnershipReleaseOutcome? CheckExplicitRelease(ViewHandle handle, out ViewInstance instance)
        {
            instance = null;
            if (IsReentrant || HasCloseEvaluation || WouldWaitForSelf(handle))
            {
                return RejectExplicitRelease(CloseStatus.Reentrant);
            }
            if (!entries.TryGetValue(handle, out instance))
            {
                return TryGetTerminal(handle, out _)
                    ? new ExplicitOwnershipReleaseOutcome(ExplicitOwnershipReleaseStatus.AlreadyReleased)
                    : RejectExplicitRelease(IsExpired(handle) ? CloseStatus.UnknownOrExpired : CloseStatus.NotFound);
            }
            if (!ownership.HasExplicitOwner(handle))
            {
                return new ExplicitOwnershipReleaseOutcome(ExplicitOwnershipReleaseStatus.AlreadyReleased);
            }
            if (!instance.IsActive || instance.HasCloseStarted || instance.CloseRequest != null ||
                instance.IsUpdatingArgs || instance.IsRebinding || retiringDependencies.Contains(handle))
            {
                return RejectExplicitRelease(CloseStatus.Blocked);
            }
            return null;
        }

        private ExplicitOwnershipReleaseOutcome ReleaseRetainedExplicitOwnership(ViewInstance instance)
        {
            ownership.ReleaseExplicit(instance.Handle);
            instance.ExplicitFocusReleased = true;
            history.Remove(instance.Handle);
            instance.CommitVersion = ++commitVersion;
            QueueLifecycleEvent(instance, NavigationEventKind.ExplicitOwnershipReleased);
            try
            {
                DispatchLifecycleEvents();
                RecomputePresentation();
                return new ExplicitOwnershipReleaseOutcome(ExplicitOwnershipReleaseStatus.Released);
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
                return new ExplicitOwnershipReleaseOutcome(ExplicitOwnershipReleaseStatus.Released, error: error);
            }
        }

        private ExplicitOwnershipReleaseOutcome CompleteExplicitRelease(ViewHandle handle, CloseOutcome closed)
        {
            if (!entries.ContainsKey(handle) || !ownership.HasExplicitOwner(handle))
            {
                return new ExplicitOwnershipReleaseOutcome(ExplicitOwnershipReleaseStatus.Released, closed);
            }
            return new ExplicitOwnershipReleaseOutcome(closed.Status == CloseStatus.WaitCancelled
                ? ExplicitOwnershipReleaseStatus.Pending : ExplicitOwnershipReleaseStatus.Rejected, closed);
        }

        private static ExplicitOwnershipReleaseOutcome RejectExplicitRelease(CloseStatus status) =>
            new ExplicitOwnershipReleaseOutcome(ExplicitOwnershipReleaseStatus.Rejected,
                new CloseOutcome(status, cleanup: CleanupStatus.NotRequired));
    }
}
