using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>立即执行同步守卫及关闭；异步生命周期实例返回 RequiresAsync，且不改变实例。</summary>
        public CloseOutcome Close(ViewHandle handle) => CloseSynchronousTraced(handle, false, null, "Close");

        public CloseOutcome Close<TResult>(ViewHandle<TResult> handle) => Close(handle.Identity);

        /// <summary>跳过业务守卫，仍要求全部资源可同步释放。</summary>
        public CloseOutcome ForceClose(ViewHandle handle) => CloseSynchronousTraced(handle, true, null, "ForceClose");

        /// <summary>通过同步守卫后发布业务结果并关闭，拒绝时不写入结果。</summary>
        public CloseOutcome Complete<TResult>(ViewHandle<TResult> handle, TResult result) =>
            CloseSynchronousTraced(handle.Identity, false, instance => instance.CreateCompletion(result), "Complete");

        private CloseOutcome CloseSynchronousCore(ViewHandle handle, bool force,
            Func<ViewInstance, Action> createCompletion)
        {
            AssertThread();
            if (IsReentrant || HasCloseEvaluation || WouldWaitForSelf(handle))
            {
                return new CloseOutcome(CloseStatus.Reentrant, cleanup: CleanupStatus.NotRequired);
            }

            if (entries.TryGetValue(handle, out var instance))
            {
                if (instance.Mode != LifetimeMode.Synchronous)
                {
                    return new CloseOutcome(CloseStatus.RequiresAsync, cleanup: CleanupStatus.NotRequired);
                }

                var accept = createCompletion == null ? null : createCompletion(instance);
                return force ? BeginCloseSynchronous(instance, DismissReason.Forced, accept)
                    : BeginRequestedCloseSynchronous(instance, DismissReason.Closed, accept);
            }

            if (TryGetTerminal(handle, out var previous))
            {
                return new CloseOutcome(CloseStatus.AlreadyClosed, previous.Error, previous.Cleanup);
            }

            return new CloseOutcome(IsExpired(handle) ? CloseStatus.UnknownOrExpired : CloseStatus.NotFound,
                cleanup: CleanupStatus.NotRequired);
        }

        private CloseOutcome BeginRequestedCloseSynchronous(ViewInstance instance, DismissReason reason,
            Action acceptResult = null)
        {
            AssertThread();
            if (HasCloseEvaluation)
            {
                return new CloseOutcome(CloseStatus.Reentrant, cleanup: CleanupStatus.NotRequired);
            }

            if (instance.CompletedCloseOutcome.HasValue)
            {
                return instance.CompletedCloseOutcome.Value;
            }

            if (instance.HasCloseStarted || instance.CloseRequest != null)
            {
                return new CloseOutcome(CloseStatus.AlreadyClosing, cleanup: CleanupStatus.NotRequired);
            }

            if (ownership.HasOwners(instance.Handle) || retiringDependencies.Contains(instance.Handle))
            {
                return new CloseOutcome(CloseStatus.InUse, cleanup: CleanupStatus.NotRequired);
            }

            var rejection = EvaluateSynchronousClose(instance, reason, acceptResult != null, out _);
            return rejection ?? BeginCloseSynchronous(instance, reason, acceptResult);
        }

        /// <summary>只求值守卫，不提交关闭；返回 null 表示当前许可成立。</summary>
        private CloseOutcome? EvaluateSynchronousClose(ViewInstance instance, DismissReason reason,
            bool hasResult, out long approvedVersion)
        {
            approvedVersion = 0;
            if (instance.State == ViewState.Open && !IsShutdown && instance.HasCloseGuard)
            {
                var guard = instance.SynchronousCloseGuard;
                if (guard == null)
                {
                    return new CloseOutcome(CloseStatus.RequiresAsync, cleanup: CleanupStatus.NotRequired);
                }

                var frame = new CloseEvaluation { Handle = instance.Handle, Parent = closeEvaluation.Value };
                closeEvaluation.Value = frame;
                try
                {
                    bool allowed;
                    using (EnterCallback(instance))
                    {
                        approvedVersion = guard.CloseVersion;
                        allowed = guard.CanClose(new CloseContext(instance.Handle, reason, hasResult));
                    }

                    long current;
                    using (EnterCallback(instance))
                    {
                        current = guard.CloseVersion;
                    }

                    if (!instance.IsActive || instance.ActivationToken.IsCancellationRequested || current != approvedVersion)
                    {
                        return new CloseOutcome(CloseStatus.Superseded, cleanup: CleanupStatus.NotRequired);
                    }

                    if (!allowed)
                    {
                        return new CloseOutcome(CloseStatus.Denied, cleanup: CleanupStatus.NotRequired);
                    }
                }
                catch (Exception error)
                {
                    return new CloseOutcome(CloseStatus.Failed, error, CleanupStatus.NotRequired);
                }
                finally
                {
                    frame.Active = false;
                    closeEvaluation.Value = frame.Parent;
                }
            }

            return null;
        }

        /// <summary>同步提交关闭并完成释放；不启动退出等待、清理超时或异步缓存工作。</summary>
        private CloseOutcome BeginCloseSynchronous(ViewInstance instance, DismissReason reason,
            Action acceptResult = null, ViewInstance replacement = null)
        {
            AssertThread();
            if (retiringDependencies.Contains(instance.Handle))
            {
                return new CloseOutcome(CloseStatus.AlreadyClosing, cleanup: CleanupStatus.NotRequired);
            }
            if (!instance.HasCloseStarted && ownership.HasOwners(instance.Handle))
            {
                return CloseOwnedDependencySynchronously(instance, reason, acceptResult, replacement);
            }
            return BeginCloseSynchronousCore(instance, reason, acceptResult, replacement);
        }

        private CloseOutcome BeginCloseSynchronousCore(ViewInstance instance, DismissReason reason,
            Action acceptResult, ViewInstance replacement)
        {
            AssertThread();
            if (instance.Mode != LifetimeMode.Synchronous)
            {
                return new CloseOutcome(CloseStatus.RequiresAsync, cleanup: CleanupStatus.NotRequired);
            }

            if (instance.HasCloseStarted)
            {
                return instance.CompletedCloseOutcome.HasValue ? instance.CompletedCloseOutcome.Value
                    : new CloseOutcome(CloseStatus.AlreadyClosing, cleanup: CleanupStatus.NotRequired);
            }

            if (!instance.CanReleaseSynchronously)
            {
                return new CloseOutcome(CloseStatus.Blocked, cleanup: CleanupStatus.NotRequired);
            }

            var trace = CurrentTraceOperation;
            acceptResult?.Invoke();
            instance.StartSynchronousClose();
            instance.BeginCleanup();
            var wasCommitted = CommitClose(instance, reason, replacement);
            var errors = new List<Exception>();
            void Attempt(Action action)
            {
                try
                {
                    using (EnterCallback(instance))
                    {
                        action();
                    }
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            Attempt(instance.FreezeBindings);
            Attempt(() => FinishEnter(instance));
            Attempt(instance.CancelActivation);
            CompleteVisualExit(instance, errors);
            DispatchLifecycleEvents();
            using (EnterCallback(instance))
            {
                instance.PublishReadinessObservers();
            }
            CloseOutcome outcome;
            using (var phase = BeginOperationPhaseTrace(instance, NavigationOperationStage.InstanceCleanup, trace))
            {
                try
                {
                    outcome = instance.Release(reason, wasCommitted, errors);
                }
                catch (Exception failure)
                {
                    errors.Add(failure);
                    var aggregate = new AggregateException("Synchronous navigation cleanup failed.", errors);
                    outcome = new CloseOutcome(CloseStatus.Failed, aggregate, CleanupStatus.Failed);
                    instance.PublishCloseResult(reason, aggregate, CleanupStatus.Failed, faulted: true);
                    instance.State = ViewState.Failed;
                }

                if (outcome.Cleanup == CleanupStatus.Complete)
                {
                    phase?.Complete();
                }
                else
                {
                    phase?.Fail(outcome.Error);
                }
            }

            instance.EndCleanup();
            CompleteClose(instance, reason, outcome, null, null);
            return outcome;
        }
    }
}
