using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    /// <summary>
    /// 单个可替换子项。在隐藏候选准备期间保留旧内容。
    /// 最多持有一个准备或退役操作，以及一个最新待处理
    /// 请求，避免不响应取消的提供方导致无界实例化。
    /// </summary>
    public sealed partial class ChildViewSlot : ISynchronousDisposable, IAsyncDisposable
    {
        private readonly ChildViewScope scope;
        private readonly AsyncLocal<PreparationFrame> preparationFrame = new AsyncLocal<PreparationFrame>();
        private readonly List<Exception> cleanupErrors = new List<Exception>();
        private readonly HashSet<ChildViewHandle> retiring = new HashSet<ChildViewHandle>();
        private Request active;
        private Request pending;
        private ChildViewHandle current;
        private TaskCompletionSource<bool> drain;
        private Task clearing;
        private TaskCompletionSource<bool> disposal;
        private Exception disposalFailure;
        private bool stopped;
        private bool committing;
        private bool accepting;
        private readonly Func<ChildViewHandle, ValueTask<bool>> retainRetired;

        public ChildViewSlot(ChildViewScope scope) : this(scope, null, null)
        {
        }

        public ChildViewSlot(ChildViewScope scope, ChildViewPreparationOptions preparationOptions)
                    : this(scope, null, preparationOptions)
        {
        }

        internal ChildViewSlot(ChildViewScope scope, Func<ChildViewHandle, ValueTask<bool>> retainRetired,
                    ChildViewPreparationOptions preparationOptions = null,
                    Func<ChildViewHandle, bool> retainRetiredSynchronous = null)
        {
            this.retainRetiredSynchronous = retainRetiredSynchronous;
            this.retainRetired = retainRetired;
            this.preparationOptions = preparationOptions ?? ChildViewPreparationOptions.Default;
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            scope.RequireActive();
            if (scope.Mode == LifetimeMode.Synchronous && (retainRetired != null || this.preparationOptions.Timeout.HasValue))
            {
                throw new InvalidOperationException("Synchronous slots cannot use asynchronous retirement callbacks or preparation timeouts.");
            }

            scope.Own(this);
        }

        public event Action<ChildViewHandle> CurrentChanged;

        /// <summary>此槽拥有该句柄的局部门控和销毁责任。</summary>
        public ChildViewHandle Current => current != null && current.IsActive ? current : null;

        /// <summary>包括保留与退役内容的回调，供上层在改变选择意图前拒绝自等待重入。</summary>
        internal bool IsExecuting
        {
            get
            {
                if (synchronousChanging || committing || accepting || (current != null && current.IsExecuting) ||
                    (preparationFrame.Value != null && preparationFrame.Value.Active))
                {
                    return true;
                }

                foreach (var handle in retiring)
                {
                    if (handle.IsExecuting)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 工厂必须返回在给定 Scope 中新准备且尚未提交的句柄。
        /// 完成结果描述选择，不表示被取代工作已完成物理清理。
        /// 不能在准备工厂内部等待本槽请求。可选验证在绑定提交前和显示前分别执行，
        /// 必须同步且无业务副作用；验证失败时回滚候选，不显示已失效的内容。
        /// </summary>
        public ValueTask<ChildViewChangeResult> ReplaceAsync(Func<ChildViewScope, CancellationToken, ValueTask<ChildViewHandle>> prepare,
            CancellationToken cancellationToken = default,
            Action<ChildViewHandle> committed = null,
            Action validateBeforeDisplay = null)
        {
            if (prepare == null)
            {
                throw new ArgumentNullException(nameof(prepare));
            }

            return Enqueue(prepare, cancellationToken, committed, validateBeforeDisplay);
        }

        public ValueTask<ChildViewChangeResult> ClearAsync(CancellationToken cancellationToken = default) => Enqueue(null, cancellationToken);

        private ValueTask<ChildViewChangeResult> Enqueue(Func<ChildViewScope, CancellationToken, ValueTask<ChildViewHandle>> prepare,
                    CancellationToken token,
                    Action<ChildViewHandle> committed = null,
                    Action validateBeforeDisplay = null,
                    Func<CancellationToken, ValueTask<RebindOutcome>> rebind = null)
        {
            scope.RequireAsyncAllowed();
            if (accepting)
            {
                throw new InvalidOperationException("Cannot reenter a slot request while its predecessor is being cancelled.");
            }

            accepting = true;
            try
            {
                return EnqueueCore(prepare, token, committed, validateBeforeDisplay, rebind);
            }
            finally
            {
                accepting = false;
            }
        }

        private ValueTask<ChildViewChangeResult> EnqueueCore(Func<ChildViewScope, CancellationToken, ValueTask<ChildViewHandle>> prepare,
                    CancellationToken token,
                    Action<ChildViewHandle> committed,
                    Action validateBeforeDisplay,
                    Func<CancellationToken, ValueTask<RebindOutcome>> rebind)
        {
            scope.RequireThread();
            if (committing)
            {
                throw new InvalidOperationException("Cannot reenter a childView slot during commit.");
            }

            if (preparationFrame.Value != null && preparationFrame.Value.Active)
            {
                throw new InvalidOperationException("Cannot enqueue a slot request from its own preparation callback.");
            }

            foreach (var handle in retiring)
            {
                if (handle.IsExecuting)
                {
                    throw new InvalidOperationException("A retiring child cannot request work that waits for its own cleanup.");
                }
            }

            if (stopped || !scope.IsActive)
            {
                return new ValueTask<ChildViewChangeResult>(new ChildViewChangeResult(ChildViewChangeStatus.ParentInactive));
            }

            if (token.IsCancellationRequested)
            {
                return new ValueTask<ChildViewChangeResult>(new ChildViewChangeResult(ChildViewChangeStatus.Cancelled));
            }

            if (pending != null)
            {
                pending.Supersede();
                pending.Dispose();
            }

            var request = new Request(scope, prepare, token, committed, validateBeforeDisplay, rebind);
            pending = request;
            if (active != null)
            {
                active.Supersede();
            }

            if (prepare == null && rebind == null)
            {
                pending = null;
                if (request.TryBeginCommit())
                {
                    var previous = current;
                    SetCurrent(null);
                    committing = true;
                    try
                    {
                        if (previous != null && previous.IsActive)
                        {
                            previous.SetLocalState(false, false);
                        }

                        request.Complete(ChildViewChangeStatus.Empty);
                    }
                    catch (Exception error)
                    {
                        request.Complete(ChildViewChangeStatus.Failed, error);
                    }
                    finally
                    {
                        committing = false;
                    }

                    if (previous != null)
                    {
                        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        clearing = finished.Task;
                        _ = FinishClearingAsync(previous, finished);
                    }
                }

                request.Dispose();
            }

            if (drain == null || drain.Task.IsCompleted)
            {
                drain = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = DrainAsync(drain);
            }

            return new ValueTask<ChildViewChangeResult>(request.Completion.Task);
        }

        private async Task DrainAsync(TaskCompletionSource<bool> completion)
        {
            try
            {
                while (true)
                {
                    if (clearing != null)
                    {
                        await clearing;
                    }
                    if (pending == null)
                    {
                        break;
                    }

                    active = pending;
                    pending = null;
                    try
                    {
                        await ApplyAsync(active);
                    }
                    catch (Exception error)
                    {
                        active.Complete(ChildViewChangeStatus.Failed, error);
                        UIErrors.Report(error);
                    }
                    finally
                    {
                        if (!active.Quarantined)
                        {
                            active.Dispose();
                        }

                        active = null;
                    }
                }

                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }

        private async Task FinishClearingAsync(ChildViewHandle handle, TaskCompletionSource<bool> finished)
        {
            try
            {
                await RetireAsync(handle);
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
                UIErrors.Report(error);
            }
            finally
            {
                finished.TrySetResult(true);
            }
        }

        private async Task ApplyAsync(Request request)
        {
            ChildViewHandle candidate = null;
            var token = request.Cancellation.Token;
            try
            {
                token.ThrowIfCancellationRequested();
                if (stopped || !scope.IsActive)
                {
                    request.Complete(ChildViewChangeStatus.ParentInactive);
                    return;
                }

                if (request.Rebind != null)
                {
                    var outcome = await InvokeRebindAsync(request, token);
                    scope.RequireThread();
                    var result = ToChangeResult(outcome);
                    request.Complete(result.Status, result.Error);
                    return;
                }

                if (request.Prepare != null)
                {
                    var prepared = await PrepareWithTimeoutAsync(request, token);
                    scope.RequireThread();
                    if (prepared == null || !prepared.BelongsTo(scope) || prepared.State != ChildViewState.Prepared)
                    {
                        throw new InvalidOperationException("Slot factory must return a Prepared handle from its supplied scope.");
                    }

                    candidate = prepared;
                    token.ThrowIfCancellationRequested();
                }

                if (request.Completion.Task.IsCompleted || stopped || !scope.IsActive)
                {
                    return;
                }

                if (!request.TryBeginCommit())
                {
                    return;
                }

                var previous = CommitCandidate(candidate, () => ValidateCommit(request, token), request.Committed);
                candidate = null;
                request.Complete(current == null ? ChildViewChangeStatus.Empty : ChildViewChangeStatus.Ready);

                // 旧命令清理前先完成选择，因为命令可能请求替换自身。
                // 在此等待退役清理，以限制持有凭证的数量。
                if (previous != null && !ReferenceEquals(previous, current))
                {
                    await RetireAsync(previous, allowRetention: true);
                }
            }
            catch (OperationCanceledException)
            {
                request.Complete(scope.IsActive && !stopped ? ChildViewChangeStatus.Cancelled : ChildViewChangeStatus.ParentInactive);
            }
            catch (Exception error)
            {
                request.Complete(scope.IsActive && !stopped ? ChildViewChangeStatus.Failed : ChildViewChangeStatus.ParentInactive, error);
                if (error is ChildViewPreparationException preparation)
                {
                    try
                    {
                        await preparation.CleanupCompletion;
                    }
                    catch (Exception cleanup)
                    {
                        cleanupErrors.Add(cleanup);
                        UIErrors.Report(cleanup);
                    }
                }
            }
            finally
            {
                if (candidate != null)
                {
                    await RetireAsync(candidate);
                }
            }
        }

        private void ValidateCommit(Request request, CancellationToken token)
        {
            request.Validate?.Invoke();
            token.ThrowIfCancellationRequested();
            if (stopped || !scope.IsActive)
            {
                throw new OperationCanceledException("Child view host became inactive before display.");
            }
        }

        private async ValueTask<ChildViewHandle> InvokePreparationAsync(Request request, CancellationToken token)
        {
            var previous = preparationFrame.Value;
            var frame = new PreparationFrame();
            preparationFrame.Value = frame;
            try
            {
                return await request.Prepare(scope, token);
            }
            finally
            {
                frame.Active = false;
                preparationFrame.Value = previous;
            }
        }

        private async Task RetireAsync(ChildViewHandle handle, bool allowRetention = false)
        {
            retiring.Add(handle);
            try
            {
                if (allowRetention && !stopped && scope.IsActive && retainRetired != null)
                {
                    try
                    {
                        if (await retainRetired(handle))
                        {
                            if (!handle.BelongsTo(scope) || handle.State != ChildViewState.Inactive)
                            {
                                throw new InvalidOperationException("Retained slot content must be inactive in the original scope.");
                            }

                            return;
                        }
                    }
                    catch (Exception error)
                    {
                        cleanupErrors.Add(error);
                        UIErrors.Report(error);
                    }
                }

                // 缓存未接管或接管失败，原槽继续负责最终关闭。
                await handle.BeginClose();
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
                UIErrors.Report(error);
            }
            finally
            {
                retiring.Remove(handle);
            }
        }

        private void SetCurrent(ChildViewHandle value)
        {
            if (ReferenceEquals(current, value))
            {
                return;
            }

            if (current != null)
            {
                current.Closed -= OnCurrentClosed;
            }

            current = value;
            if (current != null)
            {
                current.Closed += OnCurrentClosed;
            }

            var handlers = CurrentChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<ChildViewHandle> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(Current);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        private void OnCurrentClosed(ChildViewHandle handle)
        {
            if (ReferenceEquals(current, handle))
            {
                SetCurrent(null);
            }
        }

        public ValueTask DisposeAsync()
        {
            scope.RequireThread();
            if (scope.Mode == LifetimeMode.Synchronous)
            {
                try
                {
                    Dispose();
                    return default;
                }
                catch (Exception error)
                {
                    return new ValueTask(Task.FromException(error));
                }
            }

            foreach (var handle in retiring)
            {
                if (handle.IsExecuting)
                {
                    throw new InvalidOperationException("A retiring child cannot await its own slot cleanup.");
                }
            }

            if (committing || (current != null && current.IsExecuting) || (preparationFrame.Value != null && preparationFrame.Value.Active))
            {
                throw new InvalidOperationException("Cannot await slot cleanup from its commit or current child callback.");
            }

            if (disposal != null)
            {
                return new ValueTask(disposal.Task);
            }

            disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            stopped = true;
            if (pending != null)
            {
                pending.Complete(ChildViewChangeStatus.ParentInactive);
                pending.Supersede();
                pending.Dispose();
                pending = null;
            }

            if (active != null)
            {
                active.Complete(ChildViewChangeStatus.ParentInactive);
                active.Supersede();
            }

            _ = DisposeCoreAsync();
            return new ValueTask(disposal.Task);
        }

        private async Task DisposeCoreAsync()
        {
            try
            {
                if (drain != null)
                {
                    try
                    {
                        await drain.Task;
                    }
                    catch (Exception error)
                    {
                        cleanupErrors.Add(error);
                    }
                }

                var previous = current;
                SetCurrent(null);
                if (previous != null)
                {
                    await RetireAsync(previous);
                }

                await DrainQuarantinedAsync();

                if (cleanupErrors.Count != 0)
                {
                    throw new AggregateException("ChildView slot cleanup failed.", cleanupErrors);
                }

                disposal.TrySetResult(true);
            }
            catch (Exception error)
            {
                disposalFailure = error;
                disposal.TrySetException(error);
            }
            finally
            {
                CurrentChanged = null;
            }
        }

        private sealed class Request : IDisposable
        {
            private readonly object gate = new object();
            private bool commitStarted;
            public bool Quarantined;
            public readonly Func<ChildViewScope, CancellationToken, ValueTask<ChildViewHandle>> Prepare;
            public readonly Action<ChildViewHandle> Committed;
            public readonly Action Validate;
            public readonly Func<CancellationToken, ValueTask<RebindOutcome>> Rebind;
            public readonly CancellationTokenSource Cancellation;
            public readonly TaskCompletionSource<ChildViewChangeResult> Completion = new TaskCompletionSource<ChildViewChangeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationTokenRegistration registration;

            public Request(ChildViewScope scope,
                            Func<ChildViewScope, CancellationToken, ValueTask<ChildViewHandle>> prepare,
                            CancellationToken caller,
                            Action<ChildViewHandle> committed,
                            Action validate,
                            Func<CancellationToken, ValueTask<RebindOutcome>> rebind = null)
            {
                Rebind = rebind;
                Prepare = prepare;
                Committed = committed;
                Validate = validate;
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(scope.Token, caller);
                registration = Cancellation.Token.Register(() =>
                {
                    lock (gate)
                    {
                        if (!commitStarted)
                        {
                            Complete(scope.IsActive ? ChildViewChangeStatus.Cancelled : ChildViewChangeStatus.ParentInactive);
                        }
                    }
                });
            }

            public bool TryBeginCommit()
            {
                lock (gate)
                {
                    if (Completion.Task.IsCompleted || Cancellation.IsCancellationRequested)
                    {
                        return false;
                    }

                    commitStarted = true;
                    return true;
                }
            }

            public void Complete(ChildViewChangeStatus status, Exception error = null)
            {
                if (!Completion.TrySetResult(new ChildViewChangeResult(status, error)) && error != null &&
                    !ReferenceEquals(Completion.Task.Result.Error, error))
                {
                    UIErrors.Report(error);
                }
            }

            public void Supersede()
            {
                Complete(ChildViewChangeStatus.Superseded);
                try
                {
                    Cancellation.Cancel();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }

            public void Dispose()
            {
                registration.Dispose();
                Cancellation.Dispose();
            }
        }

        private sealed class PreparationFrame
        {
            public bool Active = true;
        }
    }
}
