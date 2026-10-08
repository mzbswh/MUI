using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.ChildViews
{
    /// <summary>
    /// 持有一次父激活的子视图。所有调用，包括所有者的
    /// 取消，都在所属 UI 线程执行。准备中的子项保持隐藏，
    /// 必须同时满足已提交和父级门控条件才能显示。
    /// </summary>
    public sealed partial class ChildViewScope : IAsyncDisposable, ICleanupResponsibilitySource
    {
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly SynchronizationContext synchronizationContext = SynchronizationContext.Current;
        private readonly LifetimeScope owner;
        private readonly LifetimeScope operations;
        private readonly List<ChildViewHandle> handles = new List<ChildViewHandle>();
        private readonly List<Exception> cleanupErrors = new List<Exception>();
        private readonly CancellationTokenRegistration parentCancellation;
        private TaskCompletionSource<bool> disposal;
        private Exception disposalFailure;
        private bool cleanupFinished;
        private bool hasUnknownCleanupFailure;
        private readonly List<Func<bool>> unconfirmedChildren = new List<Func<bool>>();
        private bool ended;
        private bool closeRequested;
        private bool visible;
        private bool interactable;

        /// <summary>
        /// 将子视图归父激活所有。父令牌取消时停止并隐藏子项，
        /// 子项资源保留到显式关闭或父 LifetimeScope 释放此 Scope。
        /// </summary>
        public ChildViewScope(LifetimeScope owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

            operations = new LifetimeScope();
            CleanupResponsibility = new CleanupResponsibility(ReleaseOwnedScopeAsync,
                "ChildViewScope", CompleteDisposal, true, threadId);
            owner.Own(this);
            parentCancellation = owner.Token.Register(OnParentCancelled);
        }

        public bool IsActive => disposal == null && !ended && !owner.IsEnded;

        public bool IsDisposed => disposal != null && disposal.Task.IsCompleted;

        /// <summary>
        /// 首次释放已完成且没有历史清理错误；当前实际归还状态由 IsCleanupConfirmed 表示。
        /// 未开始释放或清理尚未完成时返回 false。
        /// </summary>
        public bool IsDisposedSuccessfully => disposal != null && disposal.Task.Status == TaskStatus.RanToCompletion;

        /// <summary>当前真实归还已确认；历史任务失败不会改写，安全叶责任恢复后须显式确认本容器。</summary>
        public bool IsCleanupConfirmed => CleanupResponsibility.CaptureSnapshot().State == CleanupResponsibilityState.Completed;

        public CleanupResponsibility CleanupResponsibility
        {
            get;
        }

        public Task CleanupCompletion => disposal == null ? Task.CompletedTask : disposal.Task;

        internal bool SourcesCommitted
        {
            get; private set;
        }

        internal CancellationToken Token => owner.Token;

        public int Count
        {
            get
            {
                RequireThread();
                return handles.Count;
            }
        }

        internal void Own(IAsyncDisposable resource) => owner.Own(resource);

        /// <summary>在父级绑定提交时调用，与当前是否可见无关。</summary>
        public void CommitActivation()
        {
            RequireActive();
            if (SourcesCommitted)
            {
                return;
            }

            SourcesCommitted = true;
            foreach (var handle in handles.ToArray())
            {
                handle.CommitParentBindings();
            }
        }

        public void SetHostState(bool visible, bool interactable)
        {
            RequireActive();
            this.visible = visible;
            this.interactable = interactable;
            var errors = new List<Exception>();
            foreach (var handle in handles.ToArray())
            {
                try
                {
                    handle.SetParentState(visible, interactable);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("ChildView gates failed.", errors);
            }
        }

        public ValueTask<ChildViewHandle<TViewModel, TArgs>> PrepareAsync<TViewModel, TArgs>(ChildViewTemplate<TViewModel, TArgs> template,
                    IViewProvider provider,
                    TArgs args,
                    TViewModel assignedModel = null,
                    CancellationToken cancellationToken = default)
                    where TViewModel : ViewModel
        {
            Validate(template, provider);
            cancellationToken.ThrowIfCancellationRequested();
            return operations.RunAsync(token => PrepareCoreAsync(template, provider, args, assignedModel, token, cancellationToken));
        }

        private async ValueTask<ChildViewHandle<TViewModel, TArgs>> PrepareCoreAsync<TViewModel, TArgs>(ChildViewTemplate<TViewModel, TArgs> template,
                    IViewProvider provider,
                    TArgs args,
                    TViewModel assignedModel,
                    CancellationToken scopeToken,
                    CancellationToken callerToken)
                    where TViewModel : ViewModel
        {
            var handle = NewHandle(template, args);
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(scopeToken, owner.Token, callerToken))
            {
                using (new UIThreadCancellation(linked.Token, handle.CancelWork))
                {
                    try
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        handle.InitializeProvider(provider);
                        handle.CreateModel(assignedModel);
                        linked.Token.ThrowIfCancellationRequested();
                        // 先接管再检查取消：成功返回的迟到结果也由此处负责。
                        handle.Adopt(await provider.AcquireAsync(template.Resource, linked.Token));
                        RequireThread();
                        linked.Token.ThrowIfCancellationRequested();
                        handle.Prepare();
                        linked.Token.ThrowIfCancellationRequested();
                        await handle.PrepareAsync(linked.Token);
                        linked.Token.ThrowIfCancellationRequested();
                        handle.FinishPreparation();
                        return handle;
                    }
                    catch (Exception error)
                    {
                        try
                        {
                            await handle.BeginClose();
                        }
                        catch (Exception cleanup)
                        {
                            throw new AggregateException("ChildView preparation and rollback failed.", error, cleanup);
                        }

                        throw;
                    }
                }
            }
        }

        private ChildViewHandle<TViewModel, TArgs> NewHandle<TViewModel, TArgs>(ChildViewTemplate<TViewModel, TArgs> template, TArgs args)
                    where TViewModel : ViewModel
        {
            var handle = new ChildViewHandle<TViewModel, TArgs>(this, template, args);
            handles.Add(handle);
            handle.SetParentState(visible, interactable);
            return handle;
        }

        private void Validate<TViewModel, TArgs>(ChildViewTemplate<TViewModel, TArgs> template, IViewProvider provider)
                    where TViewModel : ViewModel
        {
            RequireActive();
            if (retainedViews != null || releasingRetainedViews)
            {
                throw new InvalidOperationException("Cannot create childViews while retaining their visuals.");
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            foreach (var handle in handles)
            {
                if (handle.IsLifecycleExecuting)
                {
                    throw new InvalidOperationException("Reentrant preparation in a childView lifecycle callback is not allowed.");
                }
            }
        }

        /// <summary>立即使状态失效，并开始关闭已准备或活动的子项，不等待完成。</summary>
        public void Cancel()
        {
            EndActivation(true);
        }

        // 父取消只终止工作；父关闭回调仍可能需要读取已经稳定的子项。
        // 显式 Cancel 或最终 Dispose 才开始子项关闭与资源释放。
        private void EndActivation(bool releaseChildren)
        {
            RequireThread();
            if (closeRequested || (ended && !releaseChildren))
            {
                return;
            }

            ended = true;
            closeRequested = releaseChildren;
            try
            {
                RefreshTicks();
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
            }

            operations.Cancel();
            foreach (var handle in handles.ToArray())
            {
                // 自定义绑定在取消命令时可能抛出异常。
                // 仍需撤销显示与输入，并使所有剩余兄弟项失效。
                try
                {
                    handle.StopForParentClose();
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }

                try
                {
                    if (releaseChildren && !handle.IsTransitioning)
                    {
                        handle.BeginCloseAndReport();
                    }
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }
            }
            // 准备操作在提供方或钩子返回前持续持有候选。
            // 取消错误保留到 Scope 最终的 DisposeAsync 结果中报告。
        }

        private void OnParentCancelled()
        {
            if (Thread.CurrentThread.ManagedThreadId == threadId)
            {
                EndActivation(false);
                return;
            }

            // 令牌回调可能在提供方或计时器线程执行。先使工作失效，
            // 所有 View 和绑定生命周期修改仍必须在 UI 上下文执行。
            operations.Cancel();
            if (synchronizationContext != null)
            {
                synchronizationContext.Post(_ =>
                {
                    try
                    {
                        EndActivation(false);
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }, null);
            }
            // 缺少上下文时，UI 所有者必须在所属线程通过 DisposeAsync 排空。
        }

        public ValueTask DisposeAsync()
        {
            RequireDisposalAllowed();
            if (disposal == null)
            {
                disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var attempt = CleanupResponsibility.DisposeAsync().AsTask();
                if (!disposal.Task.IsCompleted && attempt.IsCompleted)
                {
                    // 直接责任入口先前可能已拒绝自等待；此时观察既有首次结果，不重试清理。
                    Exception failure = null;
                    try
                    {
                        attempt.GetAwaiter().GetResult();
                    }
                    catch (Exception error)
                    {
                        failure = error;
                    }
                    CompleteDisposal(failure);
                }
            }
            return new ValueTask(disposal.Task);
        }

        private void RequireDisposalAllowed()
        {
            RequireThread();
            foreach (var handle in handles)
            {
                if (handle.IsExecuting)
                {
                    throw new InvalidOperationException("Cannot await parent cleanup from its child lifecycle or command.");
                }
            }
        }

        private void CompleteDisposal(Exception attemptFailure)
        {
            if (disposal == null || disposal.Task.IsCompleted)
            {
                return;
            }
            disposalFailure = disposalFailure ?? attemptFailure;
            if (disposalFailure == null)
            {
                disposal.TrySetResult(true);
            }
            else
            {
                disposal.TrySetException(disposalFailure);
                _ = disposal.Task.Exception;
            }
        }

        /// <summary>首次执行清理；后续显式重试只核对依赖，不重复生命周期或未知后端回调。</summary>
        private async ValueTask ReleaseOwnedScopeAsync()
        {
            if (!cleanupFinished)
            {
                RequireDisposalAllowed();
                if (disposal == null)
                {
                    disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                await DisposeCoreAsync();
            }
            if (!AreCleanupDependenciesConfirmed())
            {
                throw disposalFailure ?? new InvalidOperationException("ChildView scope cleanup dependencies are unconfirmed.");
            }
        }

        private async Task DisposeCoreAsync()
        {
            try
            {
                EndVisualRetention();
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
                hasUnknownCleanupFailure = true;
            }

            try
            {
                Cancel();
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
            }

            try
            {
                try
                {
                    await operations.DisposeAsync();
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }

                foreach (var handle in handles.ToArray())
                {
                    try
                    {
                        await handle.BeginClose();
                    }
                    catch (Exception)
                    { /* Remove 会记录每个清理错误。 */
                    }
                }
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
                hasUnknownCleanupFailure = true;
            }
            finally
            {
                try
                {
                    parentCancellation.Dispose();
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                    hasUnknownCleanupFailure = true;
                }
                cleanupFinished = true;
                disposalFailure = cleanupErrors.Count == 0 ? null :
                    new AggregateException("ChildView scope cleanup failed.", cleanupErrors);
            }
        }

        private bool AreCleanupDependenciesConfirmed()
        {
            if (!cleanupFinished || hasUnknownCleanupFailure || handles.Count != 0 || !operations.IsCleanupConfirmed)
            {
                return false;
            }
            for (var i = unconfirmedChildren.Count - 1; i >= 0; --i)
            {
                if (!unconfirmedChildren[i]())
                {
                    return false;
                }
                unconfirmedChildren.RemoveAt(i);
            }
            return true;
        }

        internal void Remove(ChildViewHandle handle, Exception failure)
        {
            handles.Remove(handle);
            if (failure != null)
            {
                cleanupErrors.Add(failure);
                var confirmed = handle.CaptureCleanupConfirmation();
                if (!confirmed())
                {
                    unconfirmedChildren.Add(confirmed);
                }
            }

            try
            {
                RefreshTicks();
            }
            catch (Exception error)
            {
                // 兄弟渲染器的 Tick 能力查询可能抛出异常，但不能阻止
                // 被移除句柄完成自身关闭操作。
                cleanupErrors.Add(error);
                UIErrors.Report(error);
            }
        }

        internal void Observe(Task cleanup)
        {
            RequireThread();
            _ = ObserveAsync(cleanup);
        }

        private static async Task ObserveAsync(Task cleanup)
        {
            try
            {
                await cleanup;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        internal void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("ChildView operations require their owning UI thread and synchronization context.");
            }
        }

        internal void RequireActive()
        {
            RequireThread();
            if (!IsActive)
            {
                throw new OperationCanceledException("Parent childView scope is inactive.", owner.Token);
            }
        }
    }
}
