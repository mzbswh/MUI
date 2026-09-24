using System;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>先同步准备隐藏候选，守卫允许后共同提交源关闭与目标打开。</summary>
        private ReplaceOutcome<TResult> ReplaceUntraced<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TArgs args, TViewModel assignedViewModel, NavigationTraceOperation trace)
            where TViewModel : ViewModel
        {
            RequireSynchronousNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            if (IsReentrant || HasCloseEvaluation || WouldWaitForSelf(source))
            {
                return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejection: ReplaceRejection.Reentrant);
            }
            if (IsShutdown || !entries.TryGetValue(source, out var current) || !CanReplace(current))
            {
                return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejection: ReplaceRejection.SourceUnavailable);
            }
            if (pending != 0 || replacing.Contains(source) || !requests.Wait(0))
            {
                return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejection: ReplaceRejection.Busy);
            }

            BeginNavigationRequest();
            try
            {
                return ReplaceSynchronousCore(current, route, args, assignedViewModel, trace);
            }
            finally
            {
                requests.Release();
                EndNavigationRequest();
            }
        }

        /// <summary>调用方持有导航许可；Open 超限替换共享本事务，不再次获取或释放许可。</summary>
        private ReplaceOutcome<TResult> ReplaceSynchronousCore<TViewModel, TArgs, TResult>(ViewInstance source,
            Route<TViewModel, TArgs, TResult> route, TArgs args, TViewModel assigned, NavigationTraceOperation trace = default)
            where TViewModel : ViewModel
        {
            if (!replacing.Add(source.Handle))
            {
                return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejection: ReplaceRejection.Busy);
            }

            ViewInstance<TViewModel, TArgs, TResult> candidate = null;
            CloseOutcome? sourceClose = null;
            var committed = false;
            ReplaceOutcome<TResult> Reject(ReplaceRejection reason, Exception error = null)
            {
                var cleanup = candidate == null ? CleanupStatus.NotRequired
                    : CloseAfterFailure(candidate, DismissReason.OpenCancelled);
                return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected,
                    new OpenOutcome<TResult>(OpenStatus.CancelledBeforeCommit, error: error, cleanup: cleanup), reason);
            }

            try
            {
                if (!CanReplace(source) || !source.CanReleaseSynchronously)
                {
                    return Reject(ReplaceRejection.SourceUnavailable);
                }
                if (!route.SupportsSynchronousLifecycle)
                {
                    return Reject(ReplaceRejection.RequiresAsync);
                }

                Register(route);
                if (!HasReplacementCapacity(route, source, null))
                {
                    return Reject(ReplaceRejection.InstanceLimit);
                }

                SyncCreateAvailability available;
                using (EnterCallback(null))
                {
                    available = HasCachedContent(route, assigned) ? SyncCreateAvailability.Available
                        : synchronousProvider.GetSyncAvailability(route.Resource);
                }
                if (available != SyncCreateAvailability.Available)
                {
                    return Reject(available == SyncCreateAvailability.RequiresPreload
                        ? ReplaceRejection.RequiresPreload : ReplaceRejection.SyncCreationUnsupported);
                }

                candidate = NewInstance(route, args, assigned);
                AssignPreparationTrace(candidate, trace);
                if (!TryPrepareCandidateSynchronously(candidate))
                {
                    return Reject(ReplaceRejection.RequiresAsync);
                }

                candidate.RequirePreparationCurrent();
                if (!CanReplace(source) || !source.CanReleaseSynchronously)
                {
                    return Reject(ReplaceRejection.Superseded);
                }
                var decision = EvaluateSynchronousClose(source, DismissReason.Replaced, false, out var version);
                if (decision.HasValue)
                {
                    return Reject(decision.Value.Status == CloseStatus.Denied ? ReplaceRejection.CloseDenied
                        : decision.Value.Status == CloseStatus.RequiresAsync ? ReplaceRejection.RequiresAsync
                        : ReplaceRejection.Superseded, decision.Value.Error);
                }

                // 提供方版本读取和守卫属性都可能调用外部代码，提交前重新确认候选与源资格。
                candidate.RequirePreparationCurrent();
                var guardCurrent = true;
                using (EnterCallback(source))
                {
                    if (source.HasCloseGuard)
                    {
                        guardCurrent = source.SynchronousCloseGuard.CloseVersion == version;
                    }
                }
                // 最后一次守卫属性访问也可能使候选失效，不能只复核源实例。
                candidate.RequirePreparationCurrent();
                if (!guardCurrent || !CanReplace(source) || !source.CanReleaseSynchronously ||
                    !HasReplacementCapacity(route, source, candidate))
                {
                    return Reject(ReplaceRejection.Superseded);
                }

                RequireDependenciesCurrent(candidate);
                presentationDeferrals++;
                try
                {
                    sourceClose = BeginCloseSynchronous(source, DismissReason.Replaced, replacement: candidate);
                    committed = candidate.Order != 0;
                    if (!committed)
                    {
                        return Reject(ReplaceRejection.Superseded, sourceClose.Value.Error);
                    }
                    Activate(candidate);
                }
                finally
                {
                    // 提交后回调即使抛错或关闭了候选，也不能将事务错误报告为提交前失败。
                    committed = candidate.Order != 0;
                    presentationDeferrals--;
                    RecomputePresentation();
                }

                return new ReplaceOutcome<TResult>(ReplaceStatus.Committed, CompletedOpen(candidate),
                    synchronousSourceCleanup: source.SynchronousCloseCompletion, sourceClose: sourceClose);
            }
            catch (NavigationPreparationRejectedException error) when (!committed)
            {
                if (candidate != null)
                {
                    CloseAfterFailure(candidate, DismissReason.OpenCancelled);
                }
                var rejected = new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: error.Rejection,
                    error: error, cleanup: InstanceCleanupState(candidate));
                return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejected, ToReplacementRejection(error.Rejection));
            }
            catch (Exception error)
            {
                var cancelled = error is OperationCanceledException;
                var cleanup = CleanupStatus.NotRequired;
                if (candidate != null)
                {
                    if (!cancelled || committed)
                    {
                        candidate.SetFailure(error);
                    }
                    cleanup = CloseAfterFailure(candidate, cancelled ? DismissReason.OpenCancelled : DismissReason.OpenFailed);
                }
                if (!cancelled)
                {
                    UIErrors.Report(error);
                }
                sourceClose = sourceClose ?? source.CompletedCloseOutcome;
                var destination = new OpenOutcome<TResult>(committed ? OpenStatus.ActivationFailed
                    : cancelled ? OpenStatus.CancelledBeforeCommit : OpenStatus.PreparationFailed,
                    committed ? candidate.TypedHandle : default, error: error, cleanup: cleanup);
                return new ReplaceOutcome<TResult>(committed ? ReplaceStatus.Committed
                    : cancelled ? ReplaceStatus.CancelledBeforeCommit : ReplaceStatus.PreparationFailed,
                    destination, synchronousSourceCleanup: committed ? source.SynchronousCloseCompletion : null,
                    sourceClose: committed ? sourceClose : null);
            }
            finally
            {
                replacing.Remove(source.Handle);
            }
        }
    }
}
