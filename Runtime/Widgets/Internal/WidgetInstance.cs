using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Widgets
{
    internal sealed partial class WidgetInstance<TViewModel, TArgs> : WidgetHandle<TViewModel, TArgs>, IArgsUpdateHost<TArgs> where TViewModel : ViewModel
    {
        // 共用上下文槽，以归属链区分嵌套回调，避免槽数量随句柄实例增长。
        private static readonly AsyncLocal<CallbackFrame> CurrentCallback = new AsyncLocal<CallbackFrame>();
        private readonly WidgetDefinition<TViewModel, TArgs> template;
        private TArgs args;
        private readonly LifetimeScope instance;
        private LifetimeScope activation;
        private bool activationCleanupComplete;
        private Task lifecycleTransitionFinished;
        private readonly List<Exception> earlyErrors = new List<Exception>();
        private Presenter<TViewModel, TArgs, Unit> presenter;
        private ViewPresenterLifecycle<TViewModel, TArgs, Unit> lifecycle;
        private IAcquiredView ownedResource;
        private CleanupResponsibility viewRelease;
        private bool resourceAcquired;
        private Func<bool> cleanupConfirmation;
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

        internal WidgetInstance(WidgetScope owner, WidgetDefinition<TViewModel, TArgs> template, TArgs args) : base(owner)
        {
            this.template = template;
            this.args = args;
            instance = new LifetimeScope();
            activation = new LifetimeScope();
        }

        private BindingContext binding => lifecycle == null ? null : lifecycle.Binding;

        /// <summary>从共用生命周期读取模型，避免宿主另存的引用与绑定模型不一致。</summary>
        public override TViewModel ViewModel => lifecycle == null ? null : lifecycle.Model;

        public override ViewModel Model => ViewModel;

        public override ViewResource Resource => template.Resource;

        public override Task CleanupCompletion => close == null ? Task.CompletedTask : close.Task;

        public override bool IsCleanupConfirmed =>
            (State == WidgetState.Closed || State == WidgetState.Failed) &&
            cleanupConfirmation != null && cleanupConfirmation();

        internal override bool IsTransitioning => State == WidgetState.Preparing || State == WidgetState.Deactivating || committing;

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

        internal override Func<bool> CaptureCleanupConfirmation() => cleanupConfirmation ?? (() => false);

        internal void CreateModel(TViewModel assigned)
        {
            lifecycle = ViewPresenterLifecycle<TViewModel, TArgs, Unit>.Create(
                assigned, template.ModelFactory, template.PresenterFactory, instance,
                Invoke, InvokeAsync, RequirePreparationCurrent);
            presenter = lifecycle.Presenter;
            hasPresenterTick = lifecycle.HasTick;
        }

        internal void Adopt(IAcquiredView acquired)
        {
            ownedResource = acquired ?? throw new InvalidOperationException("Widget provider returned a null resource acquisition.");
            resourceAcquired = true;
            viewRelease = CleanupRegistry.GetResponsibility(ownedResource, "Widget.ViewResource");
            view = ownedResource.View;
            if (view == null || !view.IsAlive)
            {
                throw new InvalidOperationException("Widget provider returned an invalid View.");
            }

            if (view is IWidgetTickHost tickHost)
            {
                tickHost.WidgetTickActivityChanged += Owner.RefreshTicks;
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
            if (view is IWidgetHost childHost && !childHost.TryCompleteWidgetPreparation())
            {
                throw new InvalidOperationException("Required child view preparation needs the asynchronous entry point.");
            }

            // 自定义子树准备可能同步关闭或取消父级，回调返回后不能恢复旧实例资格。
            RequirePreparationCurrent();
            State = WidgetState.Prepared;
        }

        private void RequirePreparationCurrent()
        {
            RequireContentCurrent();
            Owner.RequireActive();
            activation.Token.ThrowIfCancellationRequested();
            if (State != WidgetState.Preparing || closeStarted)
            {
                throw new OperationCanceledException("Widget closed during preparation.");
            }
        }

        public override void Commit()
        {
            Owner.RequireActive();
            if (State != WidgetState.Prepared)
            {
                throw new InvalidOperationException("Widget is not prepared.");
            }

            try
            {
                committing = true;
                RequireContentCurrent();
                Owner.RequireActive();
                State = WidgetState.Active;
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
            if (State != WidgetState.Active || bindingsCommitted || !Owner.SourcesCommitted)
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
                if (view is IWidgetHost childHost)
                {
                    Invoke(childHost.CommitWidgetActivation);
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
            if (State == WidgetState.Retained || State == WidgetState.Deactivating)
            {
                throw new InvalidOperationException("Retained or deactivating content cannot change local state.");
            }

            if (State == WidgetState.Closing || State == WidgetState.Closed || State == WidgetState.Failed)
            {
                throw new ObjectDisposedException(nameof(WidgetHandle));
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
            // 父级可能在渲染回调中隐藏或关闭；仅在仍然有效的首次显示时通知。
            if (visible && IsActive && bindingsCommitted && parentVisible && localVisible)
            {
                try
                {
                    lifecycle.NotifyOpened();
                }
                catch (Exception error)
                {
                    earlyErrors.Add(error);
                    BeginCloseAndReport();
                    throw;
                }
            }
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
            State = WidgetState.Closing;
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
                throw new AggregateException("Widget deactivation failed.", errors);
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
            // 只捕获稳定清理依赖，不让父容器的失败记录继续持有业务句柄和参数。
            var instanceScope = instance;
            var activationScope = activation;
            var acquisition = viewRelease;
            var acquired = resourceAcquired;
            cleanupConfirmation = () => instanceScope.IsCleanupConfirmed && activationScope.IsCleanupConfirmed &&
                (!acquired || (acquisition != null && acquisition.CaptureSnapshot().State == CleanupResponsibilityState.Completed));
            var errors = new List<Exception>(earlyErrors);
            await EndActivationAsync(errors);

            if (lifecycle != null)
            {
                lifecycle.Destroy(errors);
            }

            await ViewInstanceCleanup.RunAsync(instance, null, errors, () =>
            {
                if (view is IWidgetTickHost tickHost)
                {
                    tickHost.WidgetTickActivityChanged -= Owner.RefreshTicks;
                }
            });

            await ViewInstanceCleanup.ReleaseViewResourceAsync(ownedResource, errors,
                () => instanceScope.IsCleanupConfirmed && activationScope.IsCleanupConfirmed);

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
            State = errors.Count == 0 ? WidgetState.Closed : WidgetState.Failed;
            var failure = errors.Count == 0 ? null : new AggregateException("Widget cleanup failed.", errors);
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
            using var diagnostic = Owner.BeginCallbackDiagnostic();
            var previous = CurrentCallback.Value;
            var frame = new CallbackFrame { Owner = this, Parent = previous };
            CurrentCallback.Value = frame;
            try
            {
                action();
            }
            catch (Exception error)
            {
                UIErrors.AttachContext(error, UIErrors.CurrentContext);
                throw;
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
            using var diagnostic = Owner.BeginCallbackDiagnostic();
            var previous = CurrentCallback.Value;
            var frame = new CallbackFrame { Owner = this, Parent = previous };
            CurrentCallback.Value = frame;
            try
            {
                await action();
            }
            catch (Exception error)
            {
                UIErrors.AttachContext(error, UIErrors.CurrentContext);
                throw;
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
            public WidgetHandle Owner;
            public CallbackFrame Parent;
            public bool Active = true;
        }
    }
}
