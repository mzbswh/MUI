using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.ChildViews
{
    /// <summary>
    /// 父界面内的视图实例句柄，负责局部状态和父级所有权，不拥有导航历史或顶层焦点。
    /// 与顶层页面共用 View、Presenter 和绑定生命周期，不要求业务继承专用的子视图基类。
    /// </summary>
    public abstract class ChildViewHandle : IAsyncDisposable, ICommandTarget
    {
        protected ChildViewHandle(ChildViewScope owner)
        {
            Owner = owner;
        }

        internal event Action<ChildViewHandle> Closed;

        protected ChildViewScope Owner
        {
            get;
        }

        public ChildViewState State { get; protected set; } = ChildViewState.Preparing;

        public bool IsActive => State == ChildViewState.Active && Owner.IsActive;

        /// <summary>当前资源与绑定代际是否仍兼容；读取需在所属 UI 线程，提供方错误会抛出。</summary>
        public abstract bool IsContentCurrent
        {
            get;
        }

        public abstract ViewModel Model
        {
            get;
        }

        public abstract ViewResource Resource
        {
            get;
        }

        /// <summary>等待本实例的物理清理完成；逻辑关闭后此任务仍可能未完成。</summary>
        public abstract Task CleanupCompletion
        {
            get;
        }

        internal abstract bool IsExecuting
        {
            get;
        }

        internal abstract bool IsLifecycleExecuting
        {
            get;
        }

        internal abstract bool IsTransitioning
        {
            get;
        }

        internal abstract bool LocalVisible
        {
            get;
        }

        internal abstract bool LocalInteractable
        {
            get;
        }

        internal abstract bool WantsTick
        {
            get;
        }

        internal bool BelongsTo(ChildViewScope scope) => ReferenceEquals(Owner, scope);

        protected void NotifyClosed()
        {
            var handlers = Closed;
            Closed = null;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<ChildViewHandle> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this);
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        internal abstract void Tick(float delta);

        internal abstract void CancelWork();

        internal abstract ValueTask PrepareReactivationCoreAsync(CancellationToken token);

        internal abstract ValueTask DeactivateCoreAsync(CancellationToken token);

        internal abstract void StopForParentClose();

        internal abstract IVisualRetentionView GetVisualRetentionView();

        internal abstract void EndRetainedDisplay();

        internal abstract void CommitParentBindings();

        internal abstract void SetParentState(bool visible, bool interactable);

        internal abstract Task BeginClose();

        internal abstract void BeginCloseAndReport();

        /// <summary>将已准备的子视图提交为活动子项；实际可见性仍受父级和局部门控约束。</summary>
        public abstract void Commit();

        /// <summary>设置子项局部可见性与输入限制，不能越过父级门控。</summary>
        public abstract void SetLocalState(bool visible, bool interactable);

        /// <summary>
        /// 停止本次业务激活并保留当前画面，供异步替换期间展示。
        /// 不是暂停：旧激活不会自动恢复，仍须最终关闭并等待资源清理。
        /// </summary>
        public abstract void RetainAndDeactivate();

        /// <summary>开始关闭并等待资源收尾，不能从本子视图自身命令或生命周期回调等待。</summary>
        public abstract ValueTask DisposeAsync();

        /// <summary>不等待完成，可安全地从本子视图自己的命令中调用。</summary>
        public void RequestClose()
        {
            Owner.RequireThread();
            if (!IsActive)
            {
                throw new OperationCanceledException("ChildView is not active.");
            }

            BeginCloseAndReport();
        }

        /// <summary>子视图仅接受 Unit 完成值；道具点击等业务结果应通过业务回调传递。</summary>
        public void Complete<TResult>(TResult result)
        {
            if (typeof(TResult) != typeof(Unit))
            {
                throw new ArgumentException("Child childView completion accepts Unit only. Use a business callback for item selection.");
            }

            RequestClose();
        }
    }

    public sealed partial class ChildViewHandle<TViewModel, TArgs> : ChildViewHandle, IArgsUpdateHost<TArgs> where TViewModel : ViewModel
    {
        // 共用上下文槽，以归属链区分嵌套回调，避免槽数量随句柄实例增长。
        private static readonly AsyncLocal<CallbackFrame> CurrentCallback = new AsyncLocal<CallbackFrame>();
        private readonly ChildViewTemplate<TViewModel, TArgs> template;
        private TArgs args;
        private readonly LifetimeScope instance;
        private LifetimeScope activation;
        private bool activationCleanupComplete;
        private Task lifecycleTransitionFinished;
        private readonly List<Exception> earlyErrors = new List<Exception>();
        private Presenter<TViewModel, TArgs, Unit> presenter;
        private ViewPresenterLifecycle<TViewModel, TArgs, Unit> lifecycle;
        private IAcquiredView ownedResource;
        private IView view;
        private TaskCompletionSource<bool> close;
        private bool closeStarted;
        private bool committed;
        private bool committing;
        private bool bindingsCommitted;
        private bool parentVisible;
        private bool parentInteractable;
        private bool localVisible = true;
        private bool localInteractable = true;

        internal ChildViewHandle(ChildViewScope owner, ChildViewTemplate<TViewModel, TArgs> template, TArgs args) : base(owner)
        {
            this.template = template;
            this.args = args;
            instance = new LifetimeScope();
            activation = new LifetimeScope();
        }

        private BindingContext binding => lifecycle == null ? null : lifecycle.Binding;

        /// <summary>从共用生命周期读取模型，避免宿主另存的引用与绑定模型不一致。</summary>
        public TViewModel ViewModel => lifecycle == null ? null : lifecycle.Model;

        public override ViewModel Model => ViewModel;

        public override ViewResource Resource => template.Resource;

        public override Task CleanupCompletion => close == null ? Task.CompletedTask : close.Task;

        internal override bool IsTransitioning => State == ChildViewState.Preparing || State == ChildViewState.Deactivating || committing;

        internal override bool LocalVisible => localVisible;

        internal override bool LocalInteractable => localInteractable;

        internal override bool IsLifecycleExecuting
        {
            get
            {
                for (var frame = CurrentCallback.Value; frame != null; frame = frame.Parent)
                {
                    if (frame.Active && ReferenceEquals(frame.Owner, this))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal override bool IsExecuting
        {
            get
            {
                if (IsLifecycleExecuting)
                {
                    return true;
                }

                for (var command = CommandContext.Current; command != null; command = command.ExecutionParent)
                {
                    if (command.IsRunning && ReferenceEquals(command.Source, this))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal void CreateModel(TViewModel assigned)
        {
            lifecycle = ViewPresenterLifecycle<TViewModel, TArgs, Unit>.Create(
                assigned, template.ModelFactory, template.PresenterFactory, instance,
                Invoke, InvokeAsync, RequirePreparationCurrent);
            presenter = lifecycle.Presenter;
            hasPresenterTick = lifecycle.HasTick;
            tickInterval = lifecycle.TickInterval;

        }

        internal void Adopt(IAcquiredView acquired)
        {
            ownedResource = acquired ?? throw new InvalidOperationException("ChildView provider returned a null resource acquisition.");
            view = ownedResource.View;
            if (view == null || !view.IsAlive)
            {
                throw new InvalidOperationException("ChildView provider returned an invalid View.");
            }

            if (view is IChildTickHost tickHost)
            {
                tickHost.ChildTickActivityChanged += Owner.RefreshTicks;
            }
        }

        internal void Prepare()
        {
            lifecycle.Prepare(view, activation, args, this, template.BindingFactory, RequirePreparationCurrent);
        }

        internal ValueTask PrepareAsync(CancellationToken token)
        {
            return lifecycle.PrepareAsync(view, args, RequirePreparationCurrent, token);
        }

        internal void FinishPreparation()
        {
            RequirePreparationCurrent();
            if (view is IChildViewHost childHost && !childHost.TryCompleteChildPreparation())
            {
                throw new InvalidOperationException("Required child view preparation needs the asynchronous entry point.");
            }

            // 自定义子树准备可能同步关闭或取消父级，回调返回后不能恢复旧实例资格。
            RequirePreparationCurrent();
            State = ChildViewState.Prepared;
        }

        private void RequirePreparationCurrent()
        {
            RequireContentCurrent();
            Owner.RequireActive();
            activation.Token.ThrowIfCancellationRequested();
            if (State != ChildViewState.Preparing || closeStarted)
            {
                throw new OperationCanceledException("ChildView closed during preparation.");
            }
        }

        public override void Commit()
        {
            Owner.RequireActive();
            if (State != ChildViewState.Prepared)
            {
                throw new InvalidOperationException("ChildView is not prepared.");
            }

            try
            {
                committing = true;
                RequireContentCurrent();
                Owner.RequireActive();
                State = ChildViewState.Active;
                committed = true;
                CommitParentBindings();
                Invoke(ApplyGates);
                RequireContentCurrent();
                Owner.RequireActive();
            }
            catch
            {
                committing = false;
                BeginCloseAndReport();
                throw;
            }
            finally
            {
                committing = false;
            }
        }

        internal override void CommitParentBindings()
        {
            if (State != ChildViewState.Active || bindingsCommitted || !Owner.SourcesCommitted)
            {
                return;
            }

            var wasCommitting = committing;
            committing = true;
            try
            {
                RequireContentCurrent();
                Invoke(binding.CommitSourceWrites);
                Owner.RequireActive();
                if (view is IChildViewHost childHost)
                {
                    Invoke(childHost.CommitChildActivation);
                }

                Owner.RequireActive();
                RequireContentCurrent();
                bindingsCommitted = true;
                Owner.RefreshTicks();
                ApplyGates();
                RequireContentCurrent();
            }
            catch
            {
                committing = false;
                BeginCloseAndReport();
                throw;
            }
            finally
            {
                committing = wasCommitting;
            }
        }

        public override void SetLocalState(bool visible, bool interactable)
        {
            Owner.RequireActive();
            if (State == ChildViewState.Retained || State == ChildViewState.Deactivating)
            {
                throw new InvalidOperationException("Retained or deactivating content cannot change local state.");
            }

            if (State == ChildViewState.Closing || State == ChildViewState.Closed || State == ChildViewState.Failed)
            {
                throw new ObjectDisposedException(nameof(ChildViewHandle));
            }

            localVisible = visible;
            localInteractable = interactable;
            ApplyGates();
        }

        internal override void SetParentState(bool visible, bool interactable)
        {
            parentVisible = visible;
            parentInteractable = interactable;
            ApplyGates();
        }

        private void ApplyGates()
        {
            if (view == null || !view.IsAlive)
            {
                return;
            }

            var visible = IsActive && bindingsCommitted && parentVisible && localVisible;
            view.SetHostState(visible, visible && parentInteractable && localInteractable);
        }

        public override ValueTask DisposeAsync()
        {
            Owner.RequireThread();
            if (IsExecuting)
            {
                throw new InvalidOperationException("Cannot await a childView's cleanup from its own lifecycle or command. Use RequestClose.");
            }

            return new ValueTask(BeginClose());
        }

        /// <summary>故障恢复和取消共用关闭路径，观察最终清理错误。</summary>
        internal override void BeginCloseAndReport() => Owner.Observe(BeginClose());

        internal override Task BeginClose()
        {
            StartClose();
            return CleanupCompletion;
        }

        private void StartClose()
        {
            if (closeStarted)
            {
                return;
            }

            close = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            closeStarted = true;
            State = ChildViewState.Closing;
            try
            {
                Owner.RefreshTicks();
            }
            catch (Exception error)
            {
                earlyErrors.Add(error);
            }

            try
            {
                StopForParentClose();
            }
            catch (Exception error)
            {
                earlyErrors.Add(error);
            }

            try
            {
                if (view != null && view.IsAlive && view is IVisualRetentionView retained)
                {
                    Invoke(retained.EndVisualRetention);
                }
            }
            catch (Exception error)
            {
                earlyErrors.Add(error);
            }

            // 当前条目的命令可以请求关闭；清理不能继承命令的自等待标记。
            // 原命令仍登记在绑定生命周期中，最终释放必须等待它退出。
            _ = LifetimeScope.StartIndependentCleanup(ReleaseAfterTransitionAsync);
        }


        internal override IVisualRetentionView GetVisualRetentionView()
        {
            // 已 Retained 的子项持有自己的显示保留，父级不能重复取得后提前释放。
            if (!IsActive || !bindingsCommitted || committing)
            {
                return null;
            }

            if (view == null || !view.IsAlive)
            {
                throw new InvalidOperationException("An active childView View was destroyed.");
            }

            return view as IVisualRetentionView ??
                throw new InvalidOperationException("An active childView View cannot retain its visuals.");
        }

        internal override void CancelWork()
        {
            activation.Cancel();
            var currentBinding = binding;
            if (currentBinding != null)
            {
                currentBinding.CancelCommands();
            }
        }

        /// <summary>停止业务和绑定更新，保留稳定实例供父关闭回调读取。</summary>
        internal override void StopForParentClose()
        {
            var errors = new List<Exception>();
            try
            {
                // 标准绑定可在源写入回调同步关闭时终止剩余提交。
                // 自定义冻结能力仅在稳定提交之后调用。
                var currentBinding = binding;
                if (currentBinding != null)
                {
                    Invoke(() =>
                    {
                        if (!currentBinding.TryFreezeForClose() && bindingsCommitted && !committing &&
                            currentBinding.State == BindingState.Bound &&
                            currentBinding is IFreezableBindingContext freezable)
                        {
                            freezable.Freeze();
                        }
                    });
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                // 冻结先于取消，避免取消回调发布的模型状态改变退役画面。
                CancelWork();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                SetParentState(false, false);
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("ChildView deactivation failed.", errors);
            }
        }

        // 关闭可以立即撤销资格，但停用或恢复过程结束前不能并发销毁其 View 或 Presenter。
        private async Task ReleaseAfterTransitionAsync()
        {
            if (lifecycleTransitionFinished != null)
            {
                await lifecycleTransitionFinished;
            }
            await ReleaseAsync();
        }

        private async Task EndActivationAsync(List<Exception> errors)
        {
            if (argsUpdate != null)
            {
                var updated = await argsUpdate.Completion;
                if ((updated.ViewFaulted || updated.Cleanup == ArgsUpdateCleanup.Failed) &&
                    updated.Error != null && !errors.Contains(updated.Error))
                {
                    errors.Add(updated.Error);
                }
            }

            if (argsUpdateCleanupFailure != null && !errors.Contains(argsUpdateCleanupFailure))
            {
                errors.Add(argsUpdateCleanupFailure);
            }

            if (activationCleanupComplete)
            {
                return;
            }

            if (lifecycle != null)
            {
                await lifecycle.EndActivationAsync(activation, committed, CancellationToken.None, errors);
            }
            else
            {
                await ViewActivationCleanup.RunAsync(null, null, null, activation, errors);
            }

            bindingsCommitted = false;
            committed = false;
            activationCleanupComplete = true;
        }

        private async Task ReleaseAsync()
        {
            var errors = new List<Exception>(earlyErrors);
            await EndActivationAsync(errors);

            if (lifecycle != null)
            {
                lifecycle.Destroy(errors);
            }

            await ViewInstanceCleanup.RunAsync(instance, ownedResource, errors, () =>
            {
                if (view is IChildTickHost tickHost)
                {
                    tickHost.ChildTickActivityChanged -= Owner.RefreshTicks;
                }
            });

            CompleteRelease(errors);
        }

        private void CompleteRelease(List<Exception> errors)
        {
            ownedResource = null;
            view = null;
            lifecycle = null;
            presenter = null;
            argsUpdate = null;
            ReleaseContentVersion();
            State = errors.Count == 0 ? ChildViewState.Closed : ChildViewState.Failed;
            var failure = errors.Count == 0 ? null : new AggregateException("ChildView cleanup failed.", errors);
            Owner.Remove(this, failure);
            if (close != null && errors.Count == 0)
            {
                close.TrySetResult(true);
            }
            else if (close != null)
            {
                close.TrySetException(failure);
                _ = close.Task.Exception;
            }

            NotifyClosed();
        }

        private void Invoke(Action action)
        {
            var previous = CurrentCallback.Value;
            var frame = new CallbackFrame { Owner = this, Parent = previous };
            CurrentCallback.Value = frame;
            try
            {
                action();
            }
            finally
            {
                frame.Active = false;
                frame.Owner = null;
                CurrentCallback.Value = previous;
            }
        }

        private async ValueTask InvokeAsync(Func<ValueTask> action)
        {
            var previous = CurrentCallback.Value;
            var frame = new CallbackFrame { Owner = this, Parent = previous };
            CurrentCallback.Value = frame;
            try
            {
                await action();
            }
            finally
            {
                frame.Active = false;
                frame.Owner = null;
                CurrentCallback.Value = previous;
            }
        }

        private sealed class CallbackFrame
        {
            public ChildViewHandle Owner;
            public CallbackFrame Parent;
            public bool Active = true;
        }
    }
}
