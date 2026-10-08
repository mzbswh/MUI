using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly HashSet<ViewHandle> replacing = new HashSet<ViewHandle>();

        private ValueTask<ReplaceOutcome<TResult>> ReplaceAsyncUntraced<TViewModel, TArgs, TResult>(ViewHandle source,
                    Route<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    CancellationToken cancellationToken,
                    TViewModel assignedViewModel, NavigationTraceOperation trace)
                    where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (IsReentrant || WouldWaitForSelf(source))
            {
                return ReplacementRejected<TResult>(ReplaceRejection.Reentrant);
            }

            if (detachedCloseWaits.Contains(source))
            {
                return ReplacementRejected<TResult>(ReplaceRejection.Busy);
            }

            if (IsShutdown || !entries.TryGetValue(source, out var current) || !CanReplace(current))
            {
                return ReplacementRejected<TResult>(ReplaceRejection.SourceUnavailable);
            }

            if (pending >= queueCapacity || !replacing.Add(source))
            {
                return ReplacementRejected<TResult>(ReplaceRejection.Busy);
            }

            BeginNavigationRequest();
            return new ValueTask<ReplaceOutcome<TResult>>(ReplaceCoreAsync(current, route, args, cancellationToken, assignedViewModel, trace: trace));
        }

        private static ValueTask<ReplaceOutcome<TResult>> ReplacementRejected<TResult>(ReplaceRejection reason) => new ValueTask<ReplaceOutcome<TResult>>(new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejection: reason));

        private bool CanReplace(ViewInstance source) => source.IsActive && !source.HasCloseStarted && !ownership.HasOwners(source.Handle) &&
                    !retiringDependencies.Contains(source.Handle) && source.CloseRequest == null &&
                    !detachedCloseWaits.Contains(source.Handle) && !source.ActivationToken.IsCancellationRequested;

        // 候选可借用源界面的实例名额；其余活动与准备中的实例仍计入容量。
        private bool HasReplacementCapacity(Route route, ViewInstance source, ViewInstance candidate)
        {
            var count = 0;
            foreach (var entry in entries.Values)
            {
                if (ReferenceEquals(entry.Route, route) && OccupiesInstanceSlot(entry) &&
                    !ReferenceEquals(entry, source) && !ReferenceEquals(entry, candidate))
                {
                    count++;
                }
            }

            return count < route.Policy.MaxInstances;
        }

        private async Task<ReplaceOutcome<TResult>> ReplaceCoreAsync<TViewModel, TArgs, TResult>(ViewInstance source,
                    Route<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    CancellationToken token,
                    TViewModel assigned,
                    bool queueAcquired = false,
                    bool ownsRequest = true, NavigationTraceOperation trace = default)
                    where TViewModel : ViewModel
        {
            var acquired = queueAcquired;
            var committed = false;
            Task<CloseOutcome> sourceCleanup = null;
            ViewInstance<TViewModel, TArgs, TResult> candidate = null;
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, shutdown.Token, source.ActivationToken))
            {
                try
                {
                    if (!acquired)
                    {
                        await requests.WaitAsync(cancellation.Token);
                        acquired = true;
                    }

                    AssertThread();
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (!CanReplace(source))
                    {
                        return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejection: ReplaceRejection.SourceUnavailable);
                    }

                    Register(route);
                    if (!HasCleanupCapacity)
                    {
                        return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejection: ReplaceRejection.CleanupCapacity);
                    }

                    if (!HasReplacementCapacity(route, source, null))
                    {
                        return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejection: ReplaceRejection.InstanceLimit);
                    }

                    candidate = NewInstance(route, args, assigned);
                    candidate.ReplacementSource = source.Handle;
                    AssignPreparationTrace(candidate, trace);
                    // 源句柄的 replacing 记录及候选保持额度预留，加载和守卫不持有队列许可。
                    requests.Release();
                    acquired = false;
                    var preparationFailure = await PrepareCandidateWithDeadlineAsync(candidate, cancellation.Token);
                    if (preparationFailure != null)
                    {
                        var closing = QuarantinePreparation(candidate, preparationFailure);
                        var cancelled = preparationFailure is OperationCanceledException;
                        var destination = new OpenOutcome<TResult>(cancelled
                            ? IsShutdown ? OpenStatus.HostClosed : OpenStatus.CancelledBeforeCommit
                            : OpenStatus.PreparationFailed, error: preparationFailure, cleanup: CleanupState(closing));
                        return new ReplaceOutcome<TResult>(cancelled
                            ? ReplaceStatus.CancelledBeforeCommit : ReplaceStatus.PreparationFailed, destination);
                    }

                    cancellation.Token.ThrowIfCancellationRequested();
                    long approvedVersion = 0;
                    if (source.HasCloseGuard)
                    {
                        // 确认框可使用同一导航队列；候选保持隐藏，由当前操作继续持有。
                        var evaluation = await AwaitCloseEvaluationAsync(source,
                            activationToken => EvaluateReplacementCloseAsync(source, cancellation.Token,
                                activationToken, version => approvedVersion = version), cancellation.Token);
                        if (evaluation.Error != null)
                        {
                            var cleanup = BeginClose(candidate, DismissReason.OpenCancelled);
                            Observe(cleanup);
                            var cancelled = evaluation.Error is OperationCanceledException;
                            if (!cancelled)
                            {
                                UIErrors.Report(evaluation.Error);
                            }

                            var destination = new OpenOutcome<TResult>(OpenStatus.CancelledBeforeCommit,
                                error: evaluation.Error, cleanup: CleanupState(cleanup));
                            return new ReplaceOutcome<TResult>(cancelled
                                ? ReplaceStatus.CancelledBeforeCommit : ReplaceStatus.Rejected, destination,
                                cancelled ? ReplaceRejection.None : ReplaceRejection.CloseDecisionTimedOut);
                        }

                        var decision = evaluation.Status;
                        cancellation.Token.ThrowIfCancellationRequested();
                        if (decision != CloseStatus.Closed)
                        {
                            var rejection = decision == CloseStatus.Denied ? ReplaceRejection.CloseDenied : decision == CloseStatus.ConfirmationUnavailable ? ReplaceRejection.ConfirmationUnavailable : ReplaceRejection.Superseded;
                            ReleaseNavigationQueue(ref acquired);
                            await BeginClose(candidate, DismissReason.OpenCancelled);
                            return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, new OpenOutcome<TResult>(OpenStatus.CancelledBeforeCommit, cleanup: CleanupState(candidate.Closing)), rejection);
                        }

                    }

                    await requests.WaitAsync(cancellation.Token);
                    acquired = true;
                    AssertThread();
                    cancellation.Token.ThrowIfCancellationRequested();
                    var sourceValid = CanReplace(source);
                    if (sourceValid && source.HasCloseGuard)
                    {
                        using (EnterCallback(source))
                        {
                            sourceValid = source.CloseGuardVersion == approvedVersion;
                        }
                    }

                    // 版本属性访问器属于外部代码，返回后必须再次校验。
                    candidate.RequirePreparationCurrent();
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (!sourceValid || !CanReplace(source))
                    {
                        ReleaseNavigationQueue(ref acquired);
                        await BeginClose(candidate, DismissReason.OpenCancelled);
                        return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, new OpenOutcome<TResult>(OpenStatus.CancelledBeforeCommit, cleanup: CleanupState(candidate.Closing)), ReplaceRejection.Superseded);
                    }

                    if (!HasCleanupCapacity)
                    {
                        ReleaseNavigationQueue(ref acquired);
                        await BeginClose(candidate, DismissReason.OpenCancelled);
                        return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected,
                            new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: OpenRejection.CleanupCapacity,
                                cleanup: CleanupState(candidate.Closing)), ReplaceRejection.CleanupCapacity);
                    }

                    if (!HasReplacementCapacity(route, source, candidate))
                    {
                        ReleaseNavigationQueue(ref acquired);
                        await BeginClose(candidate, DismissReason.OpenCancelled);
                        return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected,
                            new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: OpenRejection.InstanceLimit,
                                cleanup: CleanupState(candidate.Closing)), ReplaceRejection.InstanceLimit);
                    }

                    // 移除源实例与插入候选之间不能等待异步操作或调用外部回调。
                    RequireDependenciesCurrent(candidate);
                    presentationDeferrals++;
                    try
                    {
                        committed = true;
                        _ = BeginClose(source, DismissReason.Replaced, replacement: candidate);
                        sourceCleanup = source.CleanupCompletion;
                        Observe(sourceCleanup);
                        Activate(candidate);
                    }
                    finally
                    {
                        presentationDeferrals--;
                        RecomputePresentation();
                    }

                    ReleaseNavigationQueue(ref acquired);
                    await WaitForReadinessAsync(candidate, token);
                    return new ReplaceOutcome<TResult>(ReplaceStatus.Committed, CompletedOpen(candidate), sourceCleanup: sourceCleanup);
                }
                catch (NavigationPreparationRejectedException error) when (!committed)
                {
                    if (candidate != null)
                    {
                        ReleaseNavigationQueue(ref acquired);
                        await BeginClose(candidate, DismissReason.OpenCancelled);
                    }
                    var rejected = new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: error.Rejection,
                        error: error, cleanup: InstanceCleanupState(candidate));
                    return new ReplaceOutcome<TResult>(ReplaceStatus.Rejected, rejected, ToReplacementRejection(error.Rejection));
                }
                catch (Exception error)
                {
                    var cancelled = error is OperationCanceledException;
                    if (candidate != null)
                    {
                        if (!cancelled || committed)
                        {
                            candidate.SetFailure(error);
                        }

                        ReleaseNavigationQueue(ref acquired);
                        await BeginClose(candidate, cancelled ? DismissReason.OpenCancelled : DismissReason.OpenFailed);
                    }

                    if (!cancelled)
                    {
                        UIErrors.Report(error);
                    }

                    var destination = new OpenOutcome<TResult>(committed ? OpenStatus.ActivationFailed : cancelled ? (IsShutdown ? OpenStatus.HostClosed : OpenStatus.CancelledBeforeCommit) : OpenStatus.PreparationFailed, committed && candidate != null ? candidate.TypedHandle : default, error: error, cleanup: CleanupState(candidate == null ? null : candidate.Closing));
                    return new ReplaceOutcome<TResult>(committed ? ReplaceStatus.Committed : cancelled ? ReplaceStatus.CancelledBeforeCommit : ReplaceStatus.PreparationFailed, destination, sourceCleanup: sourceCleanup);
                }
                finally
                {
                    replacing.Remove(source.Handle);
                    if (acquired)
                    {
                        requests.Release();
                    }

                    if (ownsRequest)
                    {
                        EndNavigationRequest();
                    }
                }
            }
        }

        private async ValueTask<CloseStatus> EvaluateReplacementCloseAsync(ViewInstance source,
                    CancellationToken requestToken,
                    CancellationToken activationToken,
                    Action<long> approve)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(requestToken, activationToken))
            {
                return await EvaluateDecisionAsync(source, new CloseContext(source.Handle, DismissReason.Replaced, false), linked.Token, approve);
            }
        }
    }
}
