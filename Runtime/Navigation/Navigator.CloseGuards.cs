using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly ICloseConfirmationService closeConfirmationService;
        private readonly AsyncLocal<CloseEvaluation> closeEvaluation = new AsyncLocal<CloseEvaluation>();
        private readonly HashSet<ViewHandle> detachedCloseWaits = new HashSet<ViewHandle>();

        private bool HasCloseEvaluation
        {
            get
            {
                for (var current = closeEvaluation.Value; current != null; current = current.Parent)
                {
                    if (current.Active)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private ValueTask<CloseOutcome> CompleteAsyncUntraced<TResult>(ViewHandle<TResult> handle,
                    TResult result,
                    CancellationToken cancellationToken = default)
        {
            AssertThread();
            if (WouldWaitForSelf(handle.Identity))
            {
                return BackResult(CloseStatus.Reentrant);
            }

            if (!entries.TryGetValue(handle.Identity, out var instance))
            {
                return CloseAsyncCore(handle.Identity, false, cancellationToken);
            }

            if (instance.CloseCommit.TryGetResult(out var committed))
            {
                return new ValueTask<CloseOutcome>(committed);
            }

            if (instance.HasCloseStarted)
            {
                return BackResult(CloseStatus.AlreadyClosing);
            }

            if (instance.PendingCloseIntent.HasValue || instance.CloseRequest != null)
            {
                return BackResult(CloseStatus.Busy);
            }

            if (!instance.IsActive)
            {
                return BackResult(CloseStatus.Blocked);
            }

            return new ValueTask<CloseOutcome>(WaitForCloseCommitAsync(instance,
                BeginRequestedClose(instance, DismissReason.Closed, instance.CreateCompletion(result)), cancellationToken));
        }

        private Task<CloseOutcome> BeginRequestedClose(ViewInstance instance, DismissReason reason, Action acceptResult = null)
        {
            if (instance.HasCloseStarted)
            {
                return instance.Closing;
            }

            if (instance.PendingCloseIntent.HasValue &&
                (instance.PendingCloseIntent != CloseRequestIntent.Dismiss || acceptResult != null))
            {
                return Task.FromResult(BackOutcome(CloseStatus.Busy));
            }

            if (instance.CloseRequest != null)
            {
                return instance.CloseRequest;
            }

            if (detachedCloseWaits.Contains(instance.Handle))
            {
                return Task.FromResult(new CloseOutcome(CloseStatus.Blocked, cleanup: CleanupStatus.NotRequired));
            }

            if (ownership.HasOwners(instance.Handle) || retiringDependencies.Contains(instance.Handle))
            {
                return Task.FromResult(new CloseOutcome(CloseStatus.InUse, cleanup: CleanupStatus.NotRequired));
            }

            // 准备回滚、错误处理和宿主关闭不向业务守卫请求许可。
            if (instance.State != ViewState.Open || IsShutdown || !instance.HasCloseGuard)
            {
                return BeginClose(instance, reason, acceptResult);
            }

            var completion = new TaskCompletionSource<CloseOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            instance.CloseRequest = completion.Task;
            instance.PendingCloseIntent = acceptResult == null ? CloseRequestIntent.Dismiss : CloseRequestIntent.Complete;
            using (EnterCallback(instance))
            {
                instance.CancelArgsUpdate();
                instance.CancelRebind();
            }

            _ = CheckAndCloseAsync(instance, reason, acceptResult, completion);
            return completion.Task;
        }

        private async Task CheckAndCloseAsync(ViewInstance instance,
                    DismissReason reason,
                    Action acceptResult,
                    TaskCompletionSource<CloseOutcome> completion)
        {
            CloseOutcome outcome;
            var retainCloseRequest = false;
            try
            {
                // 参数更新的候选必须先收尾，业务关闭守卫不能与其并发观察或修改模型。
                var prerequisiteError = await AwaitClosePrerequisitesAsync(instance, completion.Task);
                if (prerequisiteError != null)
                {
                    retainCloseRequest = true;
                    throw prerequisiteError;
                }

                // 只有守卫求值归激活周期管理；若在此操作内等待实际关闭，
                // 会导致生命周期清理等待自身。
                CloseApproval approval = default;
                var decision = await AwaitCloseEvaluationAsync(instance,
                    token => EvaluateDecisionAsync(instance,
                        new CloseContext(instance.Handle, reason, acceptResult != null), token,
                        approved => approval = approved), CancellationToken.None, completion.Task);
                if (decision.Error != null)
                {
                    retainCloseRequest = true;
                    throw decision.Error;
                }

                var status = decision.Status;
                AssertThread();
                if (instance.HasCloseStarted)
                {
                    outcome = await instance.Closing;
                }
                else if (!instance.IsActive)
                {
                    outcome = new CloseOutcome(CloseStatus.Superseded, cleanup: CleanupStatus.NotRequired);
                }
                else if (status != CloseStatus.Closed)
                {
                    outcome = new CloseOutcome(status, cleanup: CleanupStatus.NotRequired);
                }
                else
                {
                    // RunAsync 完成可能在后续 UI 调度中恢复，实际提交时必须复核。
                    long version;
                    using (EnterCallback(instance))
                    {
                        version = instance.CloseGuardVersion;
                    }

                    if (instance.HasCloseStarted)
                    {
                        outcome = await instance.Closing;
                    }
                    else if (ownership.HasOwners(instance.Handle) || retiringDependencies.Contains(instance.Handle))
                    {
                        outcome = new CloseOutcome(CloseStatus.InUse, cleanup: CleanupStatus.NotRequired);
                    }
                    else if (!instance.IsActive || instance.ActivationToken.IsCancellationRequested ||
                        !approval.IsCurrent(instance, version))
                    {
                        outcome = new CloseOutcome(CloseStatus.Superseded, cleanup: CleanupStatus.NotRequired);
                    }
                    else
                    {
                        outcome = await BeginClose(instance, reason, acceptResult);
                    }
                }
            }
            catch (Exception failure)
            {
                if (retainCloseRequest)
                {
                    UIErrors.Report(failure);
                    outcome = new CloseOutcome(CloseStatus.Failed, failure, CleanupStatus.NotRequired);
                }
                else if (instance.HasCloseStarted)
                {
                    outcome = await instance.Closing;
                }
                else if (failure is OperationCanceledException)
                {
                    outcome = new CloseOutcome(CloseStatus.Superseded, cleanup: CleanupStatus.NotRequired);
                }
                else
                {
                    UIErrors.Report(failure);
                    outcome = new CloseOutcome(CloseStatus.Failed, failure, CleanupStatus.NotRequired);
                }
            }

            if (!retainCloseRequest && ReferenceEquals(instance.CloseRequest, completion.Task))
            {
                instance.CloseRequest = null;
                instance.PendingCloseIntent = null;
            }

            completion.TrySetResult(outcome);
        }

        private async Task<Exception> AwaitClosePrerequisitesAsync(ViewInstance instance,
            Task<CloseOutcome> closeRequest)
        {
            var argsUpdate = instance.ArgsUpdating;
            var rebind = instance.Rebinding;
            Task pending = argsUpdate == null ? rebind : rebind == null ? argsUpdate : Task.WhenAll(argsUpdate, rebind);
            if (pending == null)
            {
                return null;
            }

            if (pending.IsCompleted)
            {
                await pending;
                return null;
            }

            using (var deadline = new CancellationTokenSource())
            {
                var elapsed = Task.Delay(instance.Route.Policy.CloseDecisionTimeout, deadline.Token);
                if (await Task.WhenAny(pending, elapsed) == pending || pending.IsCompleted)
                {
                    deadline.Cancel();
                    await pending;
                    return null;
                }
            }

            detachedCloseWaits.Add(instance.Handle);
            _ = ObserveDetachedCloseWaitAsync(instance, pending, closeRequest);
            return new TimeoutException(
                "Pending view mutation exceeded the close decision budget; the view remains owned until it finishes.");
        }

        private async Task<CloseEvaluationWait> AwaitCloseEvaluationAsync(ViewInstance instance,
            Func<CancellationToken, ValueTask<CloseStatus>> evaluate, CancellationToken waitToken,
            Task<CloseOutcome> closeRequest = null)
        {
            var decisionCancellation = CancellationTokenSource.CreateLinkedTokenSource(instance.ActivationToken, waitToken);
            var decisionToken = decisionCancellation.Token;
            var detached = false;
            try
            {
                var evaluation = instance.EvaluateCloseAsync(_ => evaluate(decisionToken)).AsTask();
                if (evaluation.IsCompleted)
                {
                    return new CloseEvaluationWait(await evaluation);
                }

                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(waitToken))
                {
                    var elapsed = Task.Delay(instance.Route.Policy.CloseDecisionTimeout, deadline.Token);
                    if (await Task.WhenAny(evaluation, elapsed) == evaluation || evaluation.IsCompleted)
                    {
                        deadline.Cancel();
                        return new CloseEvaluationWait(await evaluation);
                    }
                }

                detachedCloseWaits.Add(instance.Handle);
                detached = true;
                try
                {
                    decisionCancellation.Cancel();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }

                _ = ObserveDetachedCloseWaitAsync(instance, evaluation, closeRequest, decisionCancellation);
                if (waitToken.IsCancellationRequested)
                {
                    return new CloseEvaluationWait(new OperationCanceledException(
                        "Close decision wait was cancelled before its callback returned.", waitToken));
                }

                return new CloseEvaluationWait(new TimeoutException(
                    "Close decision exceeded its time budget; the callback remains owned until it finishes."));
            }
            finally
            {
                if (!detached)
                {
                    decisionCancellation.Dispose();
                }
            }
        }

        private async Task ObserveDetachedCloseWaitAsync(ViewInstance instance,
            Task pending, Task<CloseOutcome> closeRequest,
            CancellationTokenSource decisionCancellation = null)
        {
            try
            {
                await pending;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
            finally
            {
                decisionCancellation?.Dispose();
                detachedCloseWaits.Remove(instance.Handle);
                if (closeRequest != null && ReferenceEquals(instance.CloseRequest, closeRequest))
                {
                    instance.CloseRequest = null;
                    instance.PendingCloseIntent = null;
                }
            }
        }

        private async ValueTask<CloseStatus> EvaluateDecisionAsync(ViewInstance instance,
                    CloseContext context,
                    CancellationToken token,
                    Action<CloseApproval> approve)
        {
            var frame = new CloseEvaluation
            {
                Handle = instance.Handle,
                Parent = closeEvaluation.Value
            };
            closeEvaluation.Value = frame;
            try
            {
                token.ThrowIfCancellationRequested();
                var guard = instance.CloseGuard;
                long version;
                var commitVersion = instance.CommitVersion;
                CloseDecision decision;
                using (EnterCallback(instance))
                {
                    version = guard.CloseVersion;
                    token.ThrowIfCancellationRequested();
                    decision = await guard.CanCloseAsync(context, token);
                }

                token.ThrowIfCancellationRequested();
                if (!instance.IsActive)
                {
                    return CloseStatus.Superseded;
                }

                long evaluatedVersion;
                using (EnterCallback(instance))
                {
                    evaluatedVersion = guard.CloseVersion;
                }

                token.ThrowIfCancellationRequested();
                if (!instance.IsActive || evaluatedVersion != version || instance.CommitVersion != commitVersion)
                {
                    return CloseStatus.Superseded;
                }

                if (decision.Kind == CloseDecisionKind.Deny)
                {
                    return CloseStatus.Denied;
                }

                if (decision.Kind == CloseDecisionKind.NeedsConfirmation)
                {
                    if (closeConfirmationService == null)
                    {
                        return CloseStatus.ConfirmationUnavailable;
                    }

                    if (decision.Confirmation == null)
                    {
                        throw new InvalidOperationException("Close confirmation is missing.");
                    }

                    // 对话框运行期间不持有普通导航队列许可或回调作用域。
                    var confirmed = await closeConfirmationService.ConfirmAsync(context, decision.Confirmation, token);
                    token.ThrowIfCancellationRequested();
                    if (!confirmed)
                    {
                        return CloseStatus.Denied;
                    }
                }
                else if (decision.Kind != CloseDecisionKind.Allow)
                {
                    throw new InvalidOperationException("Invalid close decision.");
                }

                long currentVersion;
                using (EnterCallback(instance))
                {
                    currentVersion = guard.CloseVersion;
                }

                token.ThrowIfCancellationRequested();
                if (!instance.IsActive || currentVersion != version || instance.CommitVersion != commitVersion)
                {
                    return CloseStatus.Superseded;
                }

                approve(new CloseApproval(version, commitVersion));
                return CloseStatus.Closed;
            }
            finally
            {
                frame.Active = false;
                closeEvaluation.Value = frame.Parent;
            }
        }

        // 业务守卫版本与框架提交版本分别比较，参数或绑定提交不能沿用旧许可。
        private readonly struct CloseApproval
        {
            private readonly long guardVersion;
            private readonly long commitVersion;

            internal CloseApproval(long guardVersion, long commitVersion)
            {
                this.guardVersion = guardVersion;
                this.commitVersion = commitVersion;
            }

            internal bool IsCurrent(ViewInstance instance, long version) =>
                version == guardVersion && instance.CommitVersion == commitVersion;
        }

        private readonly struct CloseEvaluationWait
        {
            internal CloseEvaluationWait(CloseStatus status)
            {
                Status = status;
                Error = null;
            }

            internal CloseEvaluationWait(Exception error)
            {
                Status = default;
                Error = error;
            }

            internal CloseStatus Status
            {
                get;
            }

            internal Exception Error
            {
                get;
            }
        }

        private sealed class CloseEvaluation
        {
            internal ViewHandle Handle;
            internal CloseEvaluation Parent;
            internal bool Active = true;
        }
    }
}
