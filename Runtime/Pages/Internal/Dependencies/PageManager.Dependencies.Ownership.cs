using MUI.Pages.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        /// <summary>当前实例是否仍有显式打开关系；终态或未知句柄返回 false。</summary>
        public bool HasExplicitOwnership(PageHandle handle)
        {
            AssertThread();
            return entries.ContainsKey(handle) && ownership.HasExplicitOwner(handle);
        }

        /// <summary>异步关闭只在末拥有者撤销时发生；取消只结束本次等待，不撤回已提交的关闭。</summary>
        public ValueTask<ExplicitOwnershipReleaseOutcome> ReleaseExplicitOwnershipAsync(PageHandle handle,
            CancellationToken cancellationToken = default)
        {
            AssertThread();
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
            PageHandle handle, CancellationToken token)
        {
            var closed = await CloseAsync(handle, token);
            AssertThread();
            return CompleteExplicitRelease(handle, closed);
        }

        private ExplicitOwnershipReleaseOutcome? CheckExplicitRelease(PageHandle handle, out PageInstance instance)
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

        private ExplicitOwnershipReleaseOutcome ReleaseRetainedExplicitOwnership(PageInstance instance)
        {
            ownership.ReleaseExplicit(instance.Handle);
            instance.ExplicitFocusReleased = true;
            history.Remove(instance.Handle);
            instance.CommitVersion = ++commitVersion;
            QueueLifecycleEvent(instance, PageEventKind.ExplicitOwnershipReleased);
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

        private ExplicitOwnershipReleaseOutcome CompleteExplicitRelease(PageHandle handle, CloseOutcome closed)
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
