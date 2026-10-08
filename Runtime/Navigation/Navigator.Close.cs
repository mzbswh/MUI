using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        internal bool CanAwaitShutdown => !IsReentrant && !IsSourceCommandRunning && !HasCloseEvaluation;

        public ValueTask<CloseOutcome> CloseAsync(ViewHandle handle, CancellationToken cancellationToken = default) => CloseAsyncTraced(handle, false, cancellationToken);

        public ValueTask<CloseOutcome> ForceCloseAsync(ViewHandle handle, CancellationToken cancellationToken = default) => CloseAsyncTraced(handle, true, cancellationToken);

        private ValueTask<CloseOutcome> CloseAsyncCore(ViewHandle handle, bool force, CancellationToken cancellationToken)
        {
            AssertThread();
            if (WouldWaitForSelf(handle))
            {
                return new ValueTask<CloseOutcome>(new CloseOutcome(CloseStatus.Reentrant));
            }

            if (entries.TryGetValue(handle, out var instance))
            {
                return new ValueTask<CloseOutcome>(WaitForCloseCommitAsync(instance,
                    force ? BeginClose(instance, DismissReason.Forced) : BeginRequestedClose(instance, DismissReason.Closed), cancellationToken));
            }

            if (TryGetTerminal(handle, out var previous))
            {
                return new ValueTask<CloseOutcome>(new CloseOutcome(CloseStatus.AlreadyClosed, previous.Error, previous.Cleanup));
            }

            return new ValueTask<CloseOutcome>(new CloseOutcome(IsExpired(handle) ? CloseStatus.UnknownOrExpired : CloseStatus.NotFound));
        }

        public ValueTask<CloseOutcome> CloseAsync<TResult>(ViewHandle<TResult> handle, CancellationToken cancellationToken = default) => CloseAsync(handle.Identity, cancellationToken);

        internal void RequestClose(ViewInstance instance, DismissReason reason)
        {
            AssertThread();

            Observe(BeginRequestedClose(instance, reason));
        }

        internal void RequestCompletion(ViewInstance instance, Action acceptResult)
        {
            AssertThread();
            if (instance.State != ViewState.Open || instance.HasCloseStarted || instance.CloseRequest != null)
            {
                throw new OperationCanceledException("View already entered closing.");
            }


            Observe(BeginRequestedClose(instance, DismissReason.Closed, acceptResult));
        }

        private Task<CloseOutcome> BeginClose(ViewInstance instance,
                    DismissReason reason,
                    Action acceptResult = null,
                    ViewInstance replacement = null)
        {
            if (dependencyClosures.TryGetValue(instance.Handle, out var pendingClose))
            {
                return pendingClose;
            }
            if (!instance.HasCloseStarted && ownership.HasOwners(instance.Handle))
            {
                return CloseOwnedDependencyAsync(instance, reason, acceptResult, replacement);
            }
            return BeginCloseCore(instance, reason, acceptResult, replacement);
        }

        private Task<CloseOutcome> BeginCloseCore(ViewInstance instance, DismissReason reason,
                    Action acceptResult, ViewInstance replacement)
        {
            if (instance.HasCloseStarted)
            {
                if (reason == DismissReason.HostShutdown || reason == DismissReason.Forced)
                {
                    FinishExit(instance);
                }

                return instance.Closing;
            }


            var trace = CurrentTraceOperation;
            acceptResult?.Invoke();
            var completion = new TaskCompletionSource<CloseOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupCompletion = new TaskCompletionSource<CloseOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            instance.Closing = completion.Task;
            instance.CleanupCompletion = cleanupCompletion.Task;
            instance.BeginCleanup();
            var activationWasCommitted = instance.ActivationCommitted;
            var wasCommitted = CommitClose(instance, reason, replacement);
            var canAnimate = wasCommitted && activationWasCommitted && instance.HostVisible &&
                (reason == DismissReason.Closed || reason == DismissReason.Back || reason == DismissReason.Replaced);

            var errors = new List<Exception>();
            var visualExit = StartExit(instance, canAnimate, errors);
            try
            {
                // 先切断模型到控件的数据流，再取消业务任务；取消回调或
                // OnClose 内发布的状态不应写回已经退役的界面。
                using (EnterCallback(instance))
                {
                    instance.FreezeBindings();
                }
            }
            catch (Exception failure)
            {
                errors.Add(failure);
            }

            try
            {
                FinishEnter(instance);
            }
            catch (Exception failure)
            {
                errors.Add(failure);
            }

            try
            {
                instance.CancelActivation();
            }
            catch (Exception failure)
            {
                errors.Add(failure);
            }

            if (!instance.ExitPending)
            {
                CompleteVisualExit(instance, errors);
            }
            else
            {
                try
                {
                    RecomputePresentation();
                }
                catch (Exception failure)
                {
                    errors.Add(failure);
                    FinishExit(instance);
                }
            }

            DispatchLifecycleEvents();
            using (EnterCallback(instance))
            {
                instance.PublishReadinessObservers();
                instance.PublishResultObservers();
            }
            // RequestClose/Complete 可从绑定命令内同步提交；清理由导航器持有，不能继承该命令的自等待标记。
            _ = LifetimeScope.StartIndependentCleanup(() => FinishCloseAsync(instance, reason, wasCommitted, errors,
                completion, cleanupCompletion, visualExit, trace));
            return completion.Task;
        }

        private bool CommitClose(ViewInstance instance, DismissReason reason, ViewInstance replacement)
        {
            var wasCommitted = instance.State == ViewState.Open;
            var historyIndex = history.IndexOf(instance.Handle);
            ownership.ReleaseExplicit(instance.Handle);
            instance.State = ViewState.Closing;
            instance.ActivationCommitted = false;
            instance.EndReadiness();
            if (!instance.EnterPending && !instance.EnterFinishing)
            {
                instance.EnterTransition.TrySetResult(new ViewTransition(
                    instance.Failure == null ? ViewTransitionStatus.Interrupted : ViewTransitionStatus.Failed, instance.Failure));
            }
            instance.CommitVersion = ++commitVersion;
            tickInstances.Remove(instance);
            history.Remove(instance.Handle);
            QueueLifecycleEvent(instance, NavigationEventKind.CloseCommitted, reason);
            // 业务结果在状态提交时固定；退出动画和释放错误只进入清理结果。
            instance.PublishCloseResult(reason, instance.Failure, CleanupStatus.Pending);
            instance.CloseCommit.TrySetResult(new CloseOutcome(CloseStatus.Closed, cleanup: CleanupStatus.Pending));
            // 调用任何外部代码前，先提交双方的逻辑状态。
            if (replacement != null)
            {
                CommitOpen(replacement);
                // 替换继承旧记录的位置；新页未参与历史时只移除旧记录。
                if (historyIndex >= 0 && history.Remove(replacement.Handle))
                {
                    history.Insert(Math.Min(historyIndex, history.Count), replacement.Handle);
                }
            }

            return wasCommitted;
        }

        private async Task FinishCloseAsync(ViewInstance instance,
                    DismissReason reason,
                    bool wasCommitted,
                    List<Exception> errors,
                    TaskCompletionSource<CloseOutcome> completion,
                    TaskCompletionSource<CloseOutcome> cleanupCompletion,
                    Task visualExit,
                    NavigationTraceOperation trace)
        {
            await visualExit;
            CloseOutcome result;
            using (var phase = BeginOperationPhaseTrace(instance, NavigationOperationStage.InstanceCleanup, trace))
            {
                using (var stopDeadline = new CancellationTokenSource())
                {
                    Task deadline = Task.CompletedTask;
                    try
                    {
                        var started = System.Diagnostics.Stopwatch.GetTimestamp();
                        var release = instance.ReleaseAsync(reason, wasCommitted, errors,
                            CreateResourceReleaseTrace(instance.Handle, instance.Route.Key, trace));
                        deadline = ObserveCloseDeadlineAsync(instance, reason, release, completion, started, stopDeadline.Token);
                        result = await release;
                    }
                    catch (Exception failure)
                    {
                        errors.Add(failure);
                        result = new CloseOutcome(CloseStatus.Failed, failure, CleanupStatus.Failed);
                        instance.PublishCloseResult(reason, failure, CleanupStatus.Failed, faulted: true);
                        instance.State = ViewState.Failed;
                    }
                    finally
                    {
                        stopDeadline.Cancel();
                        await deadline;
                    }
                }

                // 取消回调可内联完成清理，再抛出错误；在取消调用完全退出后重新收集。
                if (instance.CleanupCancellationFailure != null &&
                    !ContainsCleanupError(result.Error, instance.CleanupCancellationFailure))
                {
                    var error = result.Error == null ? instance.CleanupCancellationFailure :
                        new AggregateException("Close cleanup and cancellation failed.", result.Error, instance.CleanupCancellationFailure);
                    result = new CloseOutcome(CloseStatus.Failed, error, CleanupStatus.Failed);
                    instance.State = ViewState.Failed;
                }

                if (result.Cleanup == CleanupStatus.Complete)
                {
                    phase?.Complete();
                }
                else
                {
                    phase?.Fail(result.Error);
                }
            }

            instance.EndCleanup();
            if (instance.CleanupTimedOut)
            {
                --pendingCleanupCount;
            }

            CompleteClose(instance, reason, result, completion, cleanupCompletion);
        }

        private void CompleteClose(ViewInstance instance, DismissReason reason, CloseOutcome result,
                    TaskCompletionSource<CloseOutcome> completion, TaskCompletionSource<CloseOutcome> cleanupCompletion)
        {
            if (result.Error != null && !ReferenceEquals(result.Error, instance.Failure))
            {
                UIErrors.Report(result.Error);
            }

            instance.CompletedCloseOutcome = result;
            if (result.Cleanup == CleanupStatus.Failed || result.Cleanup == CleanupStatus.Pending)
            {
                hasUnconfirmedCleanup = true;
            }
            ownership.Remove(instance.Handle);
            entries.Remove(instance.Handle);
            quarantinedPreparations.Remove(instance.Handle);
            detachedCloseWaits.Remove(instance.Handle);
            RememberTerminal(instance.Handle, result);

            QueueLifecycleEvent(instance, NavigationEventKind.Closed, reason, result);
            DispatchLifecycleEvents();
            cleanupCompletion?.TrySetResult(result);
            completion?.TrySetResult(result);
            using (EnterCallback(instance))
            {
                instance.PublishResultObservers();
            }
        }

        /// <summary>公开关闭入口等待提交或守卫拒绝；内部回滚与退出仍等待原清理任务。</summary>
        private static async Task<CloseOutcome> WaitForCloseCommitAsync(ViewInstance instance,
            Task<CloseOutcome> operation, CancellationToken token)
        {
            if (instance.CloseCommit.TryGetResult(out var committed))
            {
                return committed;
            }

            var submitted = instance.CloseCommit.Task;
            return await WaitForCloseAsync(ResolveCloseCommitAsync(instance, submitted, operation), token);
        }

        private static async Task<CloseOutcome> ResolveCloseCommitAsync(ViewInstance instance,
            Task<CloseOutcome> submitted, Task<CloseOutcome> operation)
        {
            await Task.WhenAny(submitted, operation);
            return instance.CloseCommit.TryGetResult(out var committed) ? committed : await operation;
        }

        private static async Task<CloseOutcome> WaitForCloseAsync(Task<CloseOutcome> task, CancellationToken token)
        {
            try
            {
                return await AsyncWait.WithCancellation(task, token);
            }
            catch (OperationCanceledException)
            {
                return new CloseOutcome(CloseStatus.WaitCancelled, cleanup: CleanupStatus.Pending);
            }
        }

        private static void Observe(Task<CloseOutcome> completion)
        {
            _ = ObserveCloseAsync(completion);
        }

        private static async Task ObserveCloseAsync(Task<CloseOutcome> completion)
        {
            try
            {
                await completion;
            }
            catch (Exception failure)
            {
                UIErrors.Report(failure);
            }
        }

        private ValueTask ShutdownAsyncUntraced()
        {
            AssertThread();

            if (!CanAwaitShutdown)
            {
                return new ValueTask(Task.FromException(new InvalidOperationException("A lifecycle hook or source command cannot await its own host shutdown.")));
            }

            return new ValueTask(RequestShutdownForHostUntraced());
        }

        private Task RequestShutdownForHostUntraced()
        {
            AssertThread();

            if (shutdownTask != null)
            {
                return shutdownTask;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            shutdownTask = completion.Task;
            _ = ObserveShutdownCoreAsync(completion);
            return completion.Task;
        }

        private async Task ObserveShutdownCoreAsync(TaskCompletionSource<bool> completion)
        {
            try
            {
                await ShutdownCoreAsync(completion);
            }
            catch (Exception failure)
            {
                if (!IsShutdown)
                {
                    shutdownTask = null;
                }

                completion.TrySetException(failure);
            }
        }

        private void BeginShutdownClosures(Dictionary<ViewHandle, Task<CloseOutcome>> closing,
            List<Exception> errors, bool activeOnly)
        {
            foreach (var entry in entries.Values.ToArray())
            {
                if ((activeOnly && entry.State != ViewState.Open && entry.State != ViewState.Closing) ||
                    closing.ContainsKey(entry.Handle))
                {
                    continue;
                }

                try
                {
                    closing.Add(entry.Handle, BeginCloseForCleanup(entry, DismissReason.HostShutdown));
                }
                catch (Exception failure)
                {
                    errors.Add(failure);
                }
            }
        }

        private async Task ShutdownCoreAsync(TaskCompletionSource<bool> completion)
        {
            var errors = new List<Exception>();
            try
            {
                shutdown.Cancel(throwOnFirstException: false);
            }
            catch (Exception failure)
            {
                errors.Add(failure);
                UIErrors.Report(failure);
            }

            posted.Clear();
            var preloadClearing = Task.CompletedTask;
            try
            {
                preloadClearing = StartPreloadClear();
            }
            catch (Exception failure)
            {
                errors.Add(failure);
            }

            // 先使活动 UI 失效，等待在途请求后再收拢剩余候选。
            var closing = new Dictionary<ViewHandle, Task<CloseOutcome>>();
            BeginShutdownClosures(closing, errors, activeOnly: true);
            try
            {
                // 请求等待确认期间可以释放队列许可，因此队列可用
                // 不代表候选准备或其回滚已经结束。
                await WaitForNavigationRequestsAsync();
            }
            catch (Exception failure)
            {
                errors.Add(failure);
            }

            BeginShutdownClosures(closing, errors, activeOnly: false);
            foreach (var operation in closing.Values)
            {
                try
                {
                    var result = await operation;
                    if (result.Error != null)
                    {
                        errors.Add(result.Error);
                    }
                    else if ((result.Status != CloseStatus.Closed && result.Status != CloseStatus.AlreadyClosed) ||
                        result.Cleanup != CleanupStatus.Complete)
                    {
                        errors.Add(new InvalidOperationException(
                            $"View cleanup did not complete during navigator shutdown: {result.Status}/{result.Cleanup}."));
                    }
                }
                catch (Exception failure)
                {
                    errors.Add(failure);
                }
            }

            if (entries.Count != 0)
            {
                errors.Add(new InvalidOperationException(
                    $"Navigator shutdown retained {entries.Count} view instance(s)."));
            }

            try
            {
                await FinishPreloadShutdownAsync(preloadClearing);
            }
            catch (Exception failure)
            {
                errors.Add(failure);
            }

            try
            {
                await BeginCacheClear();
            }
            catch (Exception failure)
            {
                // 本次失败通常已经登记，避免同时报告聚合异常及其重复副本。
                if (!IsRecordedCacheFailure(failure))
                {
                    errors.Add(failure);
                }
            }

            try
            {
                if (inactiveContentClearing != null)
                {
                    await inactiveContentClearing;
                }
            }
            catch (Exception failure)
            {
                if (!IsRecordedInactiveCleanup(failure))
                {
                    errors.Add(failure);
                }
            }

            // 显式清理过的失败也保留到宿主关闭，不能因缓存集合已经清空而遗失诊断。
            errors.AddRange(cacheReleaseErrors);
            cacheReleaseErrors.Clear();
            if (omittedCacheReleaseErrors != 0)
            {
                errors.Add(new InvalidOperationException(
                    $"Additional navigation cache cleanup failures omitted: {omittedCacheReleaseErrors}."));
                omittedCacheReleaseErrors = 0;
            }

            LifecycleChanged = null;
            lifecycleEvents.Clear();
            if (errors.Count == 0)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(new AggregateException("Navigator shutdown failed.", errors));
            }
        }

        public ValueTask DisposeAsync() => ShutdownAsync();
    }
}
