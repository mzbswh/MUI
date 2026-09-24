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
    public sealed partial class ChildViewScope : ISynchronousDisposable, IAsyncDisposable
    {
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly SynchronizationContext synchronizationContext = SynchronizationContext.Current;
        private readonly Lifetime owner;
        private readonly Lifetime operations;
        private readonly List<ChildViewHandle> handles = new List<ChildViewHandle>();
        private readonly List<Exception> cleanupErrors = new List<Exception>();
        private readonly CancellationTokenRegistration parentCancellation;
        private TaskCompletionSource<bool> disposal;
        private Exception disposalFailure;
        private bool synchronousDisposalStarted;
        private bool synchronousDisposalCompleted;
        private TaskCompletionSource<bool> synchronousCleanupCompletion;
        private bool ended;
        private bool closeRequested;
        private bool visible;
        private bool interactable;

        /// <summary>
        /// 将子视图归父激活所有。父令牌取消时停止并隐藏子项，
        /// 子项资源保留到显式关闭或父 Lifetime 释放此 Scope。
        /// </summary>
        public ChildViewScope(Lifetime owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            operations = new Lifetime(owner.Mode);
            owner.Own(this);
            parentCancellation = owner.Token.Register(OnParentCancelled);
        }

        public LifetimeMode Mode => owner.Mode;

        public bool IsActive => !synchronousDisposalStarted && disposal == null && !ended && !owner.IsEnded;

        public bool IsDisposed => Mode == LifetimeMode.Synchronous
                    ? synchronousDisposalCompleted
                    : disposal != null && disposal.Task.IsCompleted;

        /// <summary>
        /// 子作用域已完成释放且没有清理错误，父 View 才能开始新的激活。
        /// 同步模式直接读取最终状态，不访问或创建兼容任务；未开始释放时返回 false。
        /// </summary>
        public bool IsDisposedSuccessfully => IsDisposed && disposalFailure == null;

        public Task CleanupCompletion => Mode == LifetimeMode.Synchronous
                    ? GetSynchronousCleanupCompletion()
                    : disposal == null ? Task.CompletedTask : disposal.Task;

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

        public ChildViewHandle<TViewModel, TArgs> Prepare<TViewModel, TArgs>(ChildViewTemplate<TViewModel, TArgs> template,
                    IViewProvider provider,
                    TArgs args,
                    TViewModel assignedModel = null)
                    where TViewModel : ViewModel
        {
            if (Mode == LifetimeMode.Synchronous)
            {
                if (!(provider is ISynchronousViewProvider synchronous))
                {
                    throw new NotSupportedException("Synchronous child views require synchronous view creation and release.");
                }

                return PrepareSynchronous(template, synchronous, args, assignedModel);
            }

            Validate(template, provider);
            if (!template.SupportsSynchronousPreparation || provider.GetSyncAvailability(template.Resource) != SyncCreateAvailability.Available)
            {
                throw new InvalidOperationException("ChildView requires asynchronous preparation or preloading.");
            }

            // 工厂可能同步取消或请求销毁父级。两层所有者都保留同步操作记录，
            // 直到同步准备流程退出；失败候选继续由现有关闭协议持有。
            // 同步入口不再调用 RunAsync 或读取任务结果。
            return owner.Run(_ => operations.Run(__ => PrepareLegacySynchronous(template, provider, args, assignedModel)));
        }

        private ChildViewHandle<TViewModel, TArgs> PrepareLegacySynchronous<TViewModel, TArgs>(ChildViewTemplate<TViewModel, TArgs> template,
                    IViewProvider provider,
                    TArgs args,
                    TViewModel assignedModel)
                    where TViewModel : ViewModel
        {
            var handle = NewHandle(template, args);
            try
            {
                handle.InitializeProvider(provider);
                handle.CreateModel(assignedModel, true);
                RequireActive();
                handle.Adopt(provider.Create(template.Resource));
                RequireActive();
                handle.Prepare();
                handle.FinishPreparation();
                return handle;
            }
            catch (Exception error)
            {
                var cleanup = handle.BeginClose();
                Observe(cleanup);
                throw new ChildViewPreparationException(error, cleanup);
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
                        handle.CreateModel(assignedModel, false);
                        linked.Token.ThrowIfCancellationRequested();
                        // 先接管再检查取消：成功返回的迟到结果也由此处负责。
                        handle.Adopt(await provider.CreateAsync(template.Resource, linked.Token));
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

        private void Validate<TViewModel, TArgs>(ChildViewTemplate<TViewModel, TArgs> template, object provider)
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
            synchronousCloseRequests.Clear();
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

            if (Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("A synchronous child scope must be cancelled on its owning UI thread.");
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
            RequireThread();
            if (Mode == LifetimeMode.Synchronous)
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

            foreach (var handle in handles)
            {
                if (handle.IsExecuting)
                {
                    throw new InvalidOperationException("Cannot await parent cleanup from its child lifecycle or command.");
                }
            }

            if (disposal != null)
            {
                return new ValueTask(disposal.Task);
            }

            disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                EndVisualRetention();
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
            }

            try
            {
                Cancel();
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
            }

            // 清理任务一旦发布，必须始终存在使其完成的执行路径。
            _ = DisposeCoreAsync();
            return new ValueTask(disposal.Task);
        }

        private async Task DisposeCoreAsync()
        {
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

                if (cleanupErrors.Count != 0)
                {
                    throw new AggregateException("ChildView scope cleanup failed.", cleanupErrors);
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
                parentCancellation.Dispose();
            }
        }

        internal void Remove(ChildViewHandle handle, Exception failure)
        {
            handles.Remove(handle);
            synchronousCloseRequests.Remove(handle);
            if (failure != null)
            {
                cleanupErrors.Add(failure);
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
            RequireAsyncAllowed();
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
