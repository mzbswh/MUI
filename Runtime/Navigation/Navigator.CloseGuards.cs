using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly ICloseConfirmationService closeConfirmationService;
        private readonly AsyncLocal<CloseEvaluation> closeEvaluation = new AsyncLocal<CloseEvaluation>();

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
            RequireAsyncNavigation();
            if (WouldWaitForSelf(handle.Identity))
            {
                return BackResult(CloseStatus.Reentrant);
            }

            if (!entries.TryGetValue(handle.Identity, out var instance))
            {
                return CloseAsyncCore(handle.Identity, false, cancellationToken);
            }

            if (instance.HasCloseStarted || instance.CloseRequest != null)
            {
                return BackResult(CloseStatus.AlreadyClosing);
            }

            if (!instance.IsActive)
            {
                return BackResult(CloseStatus.Blocked);
            }

            return new ValueTask<CloseOutcome>(WaitForCloseAsync(BeginRequestedClose(instance, DismissReason.Closed, instance.CreateCompletion(result)), cancellationToken));
        }

        private Task<CloseOutcome> BeginRequestedClose(ViewInstance instance, DismissReason reason, Action acceptResult = null)
        {
            if (instance.HasCloseStarted)
            {
                return instance.Closing;
            }

            if (instance.CloseRequest != null)
            {
                return instance.CloseRequest;
            }

            if (instance.Mode == LifetimeMode.Synchronous)
            {
                return Task.FromResult(BeginRequestedCloseSynchronous(instance, reason, acceptResult));
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
            try
            {
                // 参数更新的候选必须先收尾，业务关闭守卫不能与其并发观察或修改模型。
                if (instance.ArgsUpdating != null)
                {
                    await instance.ArgsUpdating;
                }

                if (instance.Rebinding != null)
                {
                    await instance.Rebinding;
                }

                // 只有守卫求值归激活周期管理；若在此操作内等待实际关闭，
                // 会导致生命周期清理等待自身。
                long approvedVersion = 0;
                var status = await instance.EvaluateCloseAsync(token => EvaluateDecisionAsync(instance, new CloseContext(instance.Handle, reason, acceptResult != null), token, version => approvedVersion = version));
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
                    else if (!instance.IsActive || instance.ActivationToken.IsCancellationRequested || version != approvedVersion)
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
                if (instance.HasCloseStarted)
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

            if (ReferenceEquals(instance.CloseRequest, completion.Task))
            {
                instance.CloseRequest = null;
            }

            completion.TrySetResult(outcome);
        }

        private async ValueTask<CloseStatus> EvaluateDecisionAsync(ViewInstance instance,
                    CloseContext context,
                    CancellationToken token,
                    Action<long> approve)
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
                var guard = instance.Mode == LifetimeMode.Synchronous ? null : instance.CloseGuard;
                var synchronousGuard = instance.SynchronousCloseGuard;
                long version;
                CloseDecision decision;
                using (EnterCallback(instance))
                {
                    version = guard != null ? guard.CloseVersion : synchronousGuard.CloseVersion;
                    token.ThrowIfCancellationRequested();
                    decision = guard != null ? await guard.CanCloseAsync(context, token)
                        : synchronousGuard.CanClose(context) ? CloseDecision.Allow : CloseDecision.Deny;
                }

                token.ThrowIfCancellationRequested();
                if (!instance.IsActive)
                {
                    return CloseStatus.Superseded;
                }

                long evaluatedVersion;
                using (EnterCallback(instance))
                {
                    evaluatedVersion = guard != null ? guard.CloseVersion : synchronousGuard.CloseVersion;
                }

                token.ThrowIfCancellationRequested();
                if (!instance.IsActive || evaluatedVersion != version)
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
                    currentVersion = guard != null ? guard.CloseVersion : synchronousGuard.CloseVersion;
                }

                token.ThrowIfCancellationRequested();
                if (!instance.IsActive || currentVersion != version)
                {
                    return CloseStatus.Superseded;
                }

                approve(version);
                return CloseStatus.Closed;
            }
            finally
            {
                frame.Active = false;
                closeEvaluation.Value = frame.Parent;
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
