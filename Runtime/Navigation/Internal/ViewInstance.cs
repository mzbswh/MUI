using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    internal enum CloseRequestIntent
    {
        Dismiss,
        Complete,
        Replace
    }

    internal abstract partial class ViewInstance : ICommandTarget
    {
        private readonly ViewCompletion<ViewReadiness> readiness = new ViewCompletion<ViewReadiness>();
        internal ViewTickStatus LastTickStatus;
        internal readonly Dictionary<Route, DependencyResolutionKind> DependencyResolutions = new Dictionary<Route, DependencyResolutionKind>();
        internal readonly ViewCompletion<CloseOutcome> CloseCommit = new ViewCompletion<CloseOutcome>();
        internal readonly ViewCompletion<ViewTransition> EnterTransition = new ViewCompletion<ViewTransition>();
        internal readonly ViewCompletion<ViewTransition> ExitTransition = new ViewCompletion<ViewTransition>();
        internal Exception ExitTransitionError;
        internal Dictionary<Route, DependencyFailure> DependencyFailures;
        internal bool EnterPending;
        internal bool EnterFinishing;
        internal bool ExitPending;
        internal float EnterDuration;
        internal double EnterElapsed;
        internal Exception EnterDegradation;
        internal ViewHandle ParentPage;
        internal ViewHandle HiddenBy;
        internal ViewHandle BlockedBy;
        internal ViewHandle ReplacementSource;
        private Task<CloseOutcome> closing;
        public double TickRemainder;

        protected ViewInstance(Navigator owner, Route route, ViewHandle handle)
        {
            Owner = owner;
            Route = route;
            Handle = handle;
        }

        protected Navigator Owner
        {
            get;
        }

        public Route Route
        {
            get;
        }

        public ViewHandle Handle
        {
            get;
        }

        public ViewState State { get; set; } = ViewState.Opening;

        protected ViewCompletion<ViewReadiness> ReadinessResult => readiness;

        internal Task<ViewReadiness> ReadinessCompletion => readiness.Task;

        public bool ActivationCommitted
        {
            get; set;
        }

        /// <summary>是否曾提交打开；导航顺序预留不代表候选已成为活动页面。</summary>
        public bool HasOpenCommitted
        {
            get; set;
        }

        public bool Focused
        {
            get; set;
        }

        public bool Covered
        {
            get; set;
        }

        public bool HostVisible
        {
            get; set;
        }

        public bool HostInteractable
        {
            get; set;
        }

        public bool NotifiedFocused
        {
            get; set;
        }

        public bool NotifiedCovered
        {
            get; set;
        }

        /// <summary>请求接纳时确定的同层导航顺序，只能由显式置前改变。</summary>
        public long Order
        {
            get; set;
        }

        /// <summary>打开历史的位置；置前不修改，替换可继承源记录。</summary>
        public long HistoryOrder
        {
            get; set;
        }

        public long CommitVersion
        {
            get; set;
        }

        public abstract IView View
        {
            get;
        }

        public abstract ViewModel Model
        {
            get;
        }

        public abstract Exception Failure
        {
            get;
        }

        public abstract CancellationToken ActivationToken
        {
            get;
        }

        /// <summary>关闭准入只读取直接状态，不因同步关闭而创建兼容任务。</summary>
        internal bool HasCloseStarted => closing != null;

        public Task<CloseOutcome> Closing
        {
            get => closing;
            set => closing = value;
        }

        public Task<CloseOutcome> CloseRequest
        {
            get; set;
        }

        /// <summary>同一激活只接纳一个关闭意图；Replace 的确认阶段也使用此记录。</summary>
        internal CloseRequestIntent? PendingCloseIntent
        {
            get; set;
        }

        /// <summary>参数更新及候选清理的排空信号；关闭回调必须等它退出。</summary>
        internal Task<ArgsUpdateOutcome> ArgsUpdating
        {
            get; set;
        }

        internal virtual bool IsUpdatingArgs => ArgsUpdating != null && !ArgsUpdating.IsCompleted;

        internal Action CancelArgsUpdateCallback
        {
            get; set;
        }

        internal abstract Task Rebinding
        {
            get;
        }

        internal abstract bool IsRebinding
        {
            get;
        }

        internal abstract int ExecutingBindingCommandCount
        {
            get;
        }

        internal abstract bool IsExecutingBindingCommand
        {
            get;
        }

        public abstract ICloseGuard CloseGuard
        {
            get;
        }

        internal bool HasCloseGuard => CloseGuard != null;

        internal long CloseGuardVersion => CloseGuard.CloseVersion;

        public bool IsActive => State == ViewState.Open && !Owner.IsShutdown;

        public abstract bool HasTick
        {
            get;
        }

        internal int MaxTickCatchUp
        {
            get; set;
        } = 1;

        public float TickInterval
        {
            get; protected set;
        }

        /// <summary>撤销显式打开后保留依赖展示，独立焦点资格需再次显式打开才恢复。</summary>
        internal bool ExplicitFocusReleased
        {
            get; set;
        }

        internal bool PreparationComplete
        {
            get; set;
        }

        /// <summary>候选准备是否仍需取得视图资源；缓存接管后返回 false。</summary>
        internal abstract bool NeedsViewResource
        {
            get;
        }

        internal void PublishReadinessObservers() => readiness.PublishObservers();

        internal bool TryGetReadiness(out ViewReadiness result) => readiness.TryGetResult(out result);

        public void CompleteReadiness()
        {
            // 首次就绪表示激活提交完成；进入效果独立驱动，不能延迟 Open 或依赖页面的就绪。
            if (!readiness.IsCompleted && IsActive && ActivationCommitted && Failure == null &&
                Owner.AreDependenciesReady(this, out var dependencyDegradation))
            {
                var degradation = EnterDegradation ?? dependencyDegradation;
                if (EnterDegradation != null && dependencyDegradation != null &&
                    !ReferenceEquals(EnterDegradation, dependencyDegradation))
                {
                    degradation = new AggregateException("页面与必需依赖的进入效果均发生降级。",
                        EnterDegradation, dependencyDegradation);
                }
                readiness.TrySetResult(new ViewReadiness(ViewReadinessStatus.Ready, degradation, degradation != null));
            }
        }

        public void EndReadiness()
        {
            readiness.TrySetResult(new ViewReadiness(Failure == null ? ViewReadinessStatus.ClosedBeforeReady : ViewReadinessStatus.ActivationFailed, Failure));
        }

        internal void CancelArgsUpdate() => CancelArgsUpdateCallback?.Invoke();

        internal abstract void CancelRebind();

        public abstract ValueTask<CloseStatus> EvaluateCloseAsync(Func<CancellationToken, ValueTask<CloseStatus>> evaluate);

        public abstract void InvokeTick(float delta);

        internal abstract DependencyRequest ResolveDependency(int index);

        public abstract void CreateModel();

        /// <summary>校验创建线程、宿主、激活与资源版本，不改变候选所有权。</summary>
        internal abstract void RequirePreparationCurrent();

        /// <summary>记录准备过程的资源回滚错误，随激活的最终清理报告。</summary>
        internal abstract void RecordPreparationCleanupFailure(Exception error);

        /// <summary>异步模式将提供方未结束的回滚纳入候选激活清理。</summary>
        internal abstract void OwnPreparationCleanup(Task cleanup);

        protected void ChildTicksChanged() => Owner.RefreshTickRegistration(this);

        protected void InputStateChanged() => Owner.InputStateChanged(this);

        public abstract void AdoptViewResource(IAcquiredView ownedResource);

        public abstract void Prepare();

        public abstract ValueTask PrepareAsync(CancellationToken token);

        public abstract void ActivateBindings();

        public abstract void FreezeBindings();

        public abstract void CancelActivation();

        public abstract void NotifyFocus(bool focused);

        public abstract void NotifyCoverage(bool covered);

        public abstract BackResponse HandleBack();

        public abstract void SetFailure(Exception failure);

        public abstract Task<CloseOutcome> ReleaseAsync(DismissReason reason, bool wasCommitted,
            IReadOnlyList<Exception> initialErrors, Func<IViewResourceReleaseTraceScope> beginResourceRelease = null);

        public void RequestClose()
        {
            if (!IsActive)
            {
                throw new OperationCanceledException("Source View is not active.");
            }

            Owner.RequestClose(this, DismissReason.Closed);
        }

        public abstract void Complete<TResult>(TResult result);

        internal abstract void PublishResultObservers();

        public abstract Action CreateCompletion<T>(T result);
    }

    internal sealed partial class ViewInstance<TViewModel, TArgs, TResult> : ViewInstance, IArgsUpdateHost<TArgs> where TViewModel : ViewModel
    {
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly Route<TViewModel, TArgs, TResult> route;
        private readonly TViewModel assignedModel;
        private ViewContent<TViewModel, TArgs, TResult> content;
        private readonly LifetimeScope activationLifetime;
        private readonly ViewCompletion<ViewResult<TResult>> result = new ViewCompletion<ViewResult<TResult>>();
        private bool completed;
        private TResult value;
        private Exception failure;

        public ViewInstance(Navigator owner,
                    Route<TViewModel, TArgs, TResult> route,
                    ViewHandle handle,
                    TArgs args,
                    TViewModel assignedModel) : base(owner,
                    route,
                    handle)
        {
            activationLifetime = new LifetimeScope();
            this.route = route;
            Args = args;
            this.assignedModel = assignedModel;
        }

        private TViewModel model => content == null ? null : content.Model;

        private Presenter<TViewModel, TArgs, TResult> presenter => content == null ? null : content.Presenter;

        private ViewPresenterLifecycle<TViewModel, TArgs, TResult> lifecycle => content == null ? null : content.Lifecycle;

        private BindingContext binding => lifecycle == null ? null : lifecycle.Binding;

        private IView view => content == null ? null : content.View;

        public TArgs Args
        {
            get; private set;
        }

        internal IArgsUpdatePresenter<TArgs> ArgsUpdater => presenter as IArgsUpdatePresenter<TArgs>;

        public ViewHandle<TResult> TypedHandle => new ViewHandle<TResult>(Handle, result, ReadinessResult, EnterTransition, ExitTransition);

        public TViewModel TypedModel => model;

        public override ViewModel Model => model;

        public override IView View => view;

        public override CancellationToken ActivationToken => activationLifetime.Token;

        public override Exception Failure => failure;

        public override ICloseGuard CloseGuard => presenter as ICloseGuard;

        public override bool HasTick => presenter is IViewTick || presenter is ILowFrequencyViewTick;

        /// <summary>缓存命中时沿用实例视图，无需再次向提供者取得资源凭证。</summary>
        internal override bool NeedsViewResource => view == null;

        internal void SetArgs(TArgs args)
        {
            presenter.SetArgs(args);
            Args = args;
        }

        /// <summary>提供方没有交出凭证时，回滚残留仍必须进入候选的最终清理结果。</summary>
        internal override void RecordPreparationCleanupFailure(Exception error) => activationLifetime.RecordCleanupFailure(error);

        /// <summary>普通同步 Open 可在允许异步的宿主中失败返回，由候选关闭继续等待后端回滚。</summary>
        internal override void OwnPreparationCleanup(Task cleanup) => activationLifetime.OnDisposeAsync(() => new ValueTask(cleanup));

        public override ValueTask<CloseStatus> EvaluateCloseAsync(Func<CancellationToken, ValueTask<CloseStatus>> evaluate) => activationLifetime.RunAsync(evaluate);

        public override void InvokeTick(float delta)
        {
            if (IsRebinding)
            {
                return;
            }

            if (presenter is IViewTick frame)
            {
                frame.OnViewTick(delta);
            }
            else if (presenter is ILowFrequencyViewTick low)
            {
                low.OnLowFrequencyTick(delta);
            }
        }

        internal override DependencyRequest ResolveDependency(int index) => route.Dependencies[index].Resolve(Args);

        public override void CreateModel()
        {
            // 缓存只移交 View 凭证；每次打开仍创建新的模型、Presenter 和实例作用域。
            content = new ViewContent<TViewModel, TArgs, TResult>(Owner.CacheGeneration,
                BindingRegistry.Generation, Owner.CaptureProviderVersion());
            var cached = Owner.TakeCachedContent(route);
            if (cached != null)
            {
                AdoptViewResource(cached.TakeAcquisition());
            }
            content.Create(route, assignedModel, InvokeLifecycle, InvokeLifecycleAsync, RequirePreparationCurrent);

            RequirePreparationCurrent();
            TickInterval = lifecycle.TickInterval;
            MaxTickCatchUp = lifecycle.MaxTickCatchUp;
        }

        public override void AdoptViewResource(IAcquiredView acquired)
        {
            content.Adopt(acquired);
            AttachHost();
        }

        /// <summary>为新取得或缓存接管的视图连接当前导航宿主的监听。</summary>
        private void AttachHost()
        {
            if (view is IInputView input)
            {
                input.InputStateChanged += InputStateChanged;
            }

            if (view is IChildTickHost tickHost)
            {
                tickHost.ChildTickActivityChanged += ChildTicksChanged;
            }
        }

        public override void Prepare()
        {
            if (route.Policy.Modal && !(view is IModalView))
            {
                throw new InvalidOperationException("Modal routes require a renderer with IModalView support.");
            }

            if (route.Policy.BackBehavior == BackBehavior.HandleByPresenter && !(presenter is IBackHandler))
            {
                throw new InvalidOperationException("HandleByPresenter requires a Presenter implementing IBackHandler.");
            }

            lifecycle.Prepare(view, activationLifetime, Args, this, route.BindingFactory, RequirePreparationCurrent,
                BeginPreparationStepTrace);
        }

        private IViewPreparationTraceScope BeginPreparationStepTrace(ViewPreparationStep step) =>
            Owner.BeginPreparationStepTrace(this, step);

        public override async ValueTask PrepareAsync(CancellationToken token)
        {

            using (new UIThreadCancellation(token, CancelActivation))
            {
                await lifecycle.PrepareAsync(view, Args, RequirePreparationCurrent, token,
                    BeginPreparationStepTrace);
            }
        }

        private void InvokeLifecycle(Action action)
        {
            using (Owner.EnterCallback(this))
            {
                action();
            }
        }

        private async ValueTask InvokeLifecycleAsync(Func<ValueTask> action)
        {

            using (Owner.EnterCallback(this))
            {
                await action();
            }
        }

        internal override void RequirePreparationCurrent()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("View preparation requires its creating UI thread.");
            }

            if (content != null && !Owner.IsProviderVersionCurrent(content))
            {
                throw new OperationCanceledException("View provider content changed during preparation.");
            }

            // 提供方查询可执行外部代码，返回后检查绑定代际，避免提交旧注册表创建的绑定。
            if (content != null && !ReferenceEquals(content.BindingGeneration, BindingRegistry.Generation))
            {
                throw new OperationCanceledException("View bindings changed during preparation.");
            }

            activationLifetime.Token.ThrowIfCancellationRequested();
            if (Owner.IsShutdown || State != ViewState.Opening || HasCloseStarted)
            {
                throw new OperationCanceledException("View closed during preparation.");
            }
        }

        public override void ActivateBindings()
        {
            binding.CommitSourceWrites();
            // 来源设置器可能同步关闭并释放此实例。
            if (!IsActive)
            {
                return;
            }

            if (view is IChildViewHost childHost)
            {
                childHost.CommitChildActivation();
            }
        }

        public override void FreezeBindings()
        {
            if (binding != null)
            {
                binding.TryFreezeForClose();
            }
        }

        public override void CancelActivation()
        {
            activationLifetime.Cancel();
            if (binding != null)
            {
                binding.CancelCommands();
            }
        }

        public override void NotifyFocus(bool focused) => presenter.Focus(focused);

        public override void NotifyCoverage(bool covered) => presenter.Cover(covered);

        public override BackResponse HandleBack() => ((IBackHandler)presenter).HandleBack();

        public override void SetFailure(Exception error) => failure = error;

        public override void Complete<T>(T completion)
        {
            if (!IsActive)
            {
                throw new OperationCanceledException("Source View is not active.");
            }

            Owner.RequestCompletion(this, CreateCompletion(completion));
        }

        public override Action CreateCompletion<T>(T completion)
        {
            if (typeof(T) != typeof(TResult))
            {
                throw new ArgumentException($"Expected result {typeof(TResult).FullName}, got {typeof(T).FullName}.");
            }

            var candidate = (TResult)(object)completion;
            return () =>
            {
                value = candidate;
                completed = true;
            };
        }

        private void DetachHost(List<Exception> errors)
        {
            try
            {
                if (view is IInputView input)
                {
                    input.InputStateChanged -= InputStateChanged;
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                if (view is IChildTickHost ticks)
                {
                    ticks.ChildTickActivityChanged -= ChildTicksChanged;
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        public override Task<CloseOutcome> ReleaseAsync(DismissReason reason, bool wasCommitted,
            IReadOnlyList<Exception> initialErrors, Func<IViewResourceReleaseTraceScope> beginResourceRelease = null)
        {
            return ReleaseCoreAsync(reason, wasCommitted, initialErrors, beginResourceRelease);
        }

        private async Task<CloseOutcome> ReleaseCoreAsync(DismissReason reason, bool wasCommitted,
            IReadOnlyList<Exception> initialErrors, Func<IViewResourceReleaseTraceScope> beginResourceRelease)
        {
            var errors = new List<Exception>(initialErrors);
            Exception lifecycleFailure = failure;
            try
            {
                CancelActivation();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            // 先取消准备，再等待整次准备退出。迟到加载结果仍由候选接管；
            // 不能在 OnOpenAsync 或提供方仍使用内容时先销毁模型、视图和实例资源。
            if (IsPreparing)
            {
                await PreparationExecutionCompletion;
            }

            // 清理使用新令牌，打开或关闭等待者的取消不能中止清理。
            if (ArgsUpdating != null)
            {
                var updated = await ArgsUpdating;
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

            if (Rebinding != null)
            {
                await Rebinding;
            }

            // 更新回滚可能在关闭已开始后失败，必须在排空后重新读取，防止错误内容进入缓存。
            lifecycleFailure = failure ?? lifecycleFailure;

            // 独立清理令牌由关闭协调器控制；超时取消不能释放仍被业务任务持有的资源。
            if (lifecycle != null)
            {
                var closeFailure = await lifecycle.EndActivationAsync(
                    activationLifetime, wasCommitted, CleanupToken, errors);
                lifecycleFailure = lifecycleFailure ?? closeFailure;
            }
            else
            {
                await ViewActivationCleanup.RunAsync(null, null, null, activationLifetime, errors);
            }

            await Owner.ReleaseDependenciesAsync(this, errors);

            if (content != null)
            {
                DetachHost(errors);
                var retained = false;
                var cacheDecision = route.Policy.CacheMode == ViewCacheMode.None ? ViewCacheDecision.Disabled :
                    !wasCommitted || (reason != DismissReason.Closed && reason != DismissReason.Back && reason != DismissReason.Replaced)
                        ? ViewCacheDecision.AbnormalClose :
                    CleanupTimedOut || errors.Count != 0 || lifecycleFailure != null ? ViewCacheDecision.CleanupIncomplete :
                    !content.CanCache ? ViewCacheDecision.Unsupported : ViewCacheDecision.Retained;
                if (cacheDecision != ViewCacheDecision.Retained)
                {
                    Owner.RecordCacheDecision(route, cacheDecision);
                }
                if (wasCommitted && !CleanupTimedOut && errors.Count == 0 && lifecycleFailure == null &&
                    route.Policy.CacheMode != ViewCacheMode.None &&
                    (reason == DismissReason.Closed || reason == DismissReason.Back || reason == DismissReason.Replaced))
                {
                    try
                    {
                        var cached = await content.DetachForCacheAsync(errors, activationLifetime);
                        if (cached == null && cacheDecision == ViewCacheDecision.Retained)
                        {
                            Owner.RecordCacheDecision(route, ViewCacheDecision.CleanupIncomplete);
                        }
                        if (cached != null)
                        {
                            try
                            {
                                if (CleanupTimedOut)
                                {
                                    Owner.RecordCacheDecision(route, ViewCacheDecision.CleanupIncomplete);
                                }
                                retained = !CleanupTimedOut && Owner.RetainContent(route, cached);
                            }
                            finally
                            {
                                if (!retained)
                                {
                                    await cached.ReleaseCachedAsync(errors, beginResourceRelease);
                                }
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        Owner.RecordCacheDecision(route, ViewCacheDecision.ResetFailed);
                        errors.Add(error);
                    }
                }

                if (!retained)
                {
                    await content.ReleaseWithActivationAsync(errors, activationLifetime, beginResourceRelease);
                }

                content = null;
            }

            return CompleteRelease(reason, lifecycleFailure, errors);
        }

        private CloseOutcome CompleteRelease(DismissReason reason, Exception lifecycleFailure, List<Exception> errors)
        {
            if (CleanupCancellationFailure != null && !errors.Contains(CleanupCancellationFailure))
            {
                errors.Add(CleanupCancellationFailure);
            }

            var aggregate = errors.Count == 0 ? null : new AggregateException("View cleanup failed.", errors);
            var cleanup = aggregate == null ? CleanupStatus.Complete : CleanupStatus.Failed;
            Exception reported = aggregate ?? lifecycleFailure;
            if (aggregate != null && lifecycleFailure != null && !errors.Contains(lifecycleFailure))
            {
                reported = new AggregateException("View operation and cleanup failed.", lifecycleFailure, aggregate);
            }

            PublishCloseResult(reason, reported, cleanup, lifecycleFailure != null);
            State = aggregate == null && lifecycleFailure == null ? ViewState.Destroyed : ViewState.Failed;
            return new CloseOutcome(aggregate == null && lifecycleFailure == null ? CloseStatus.Closed : CloseStatus.Failed, reported, cleanup);
        }

        internal override void PublishCloseResult(DismissReason reason, Exception error, CleanupStatus cleanup, bool faulted = false)
        {
            if (result.IsCompleted)
            {
                return;
            }

            var resultStatus = faulted || failure != null ? ViewResultStatus.Faulted : completed ? ViewResultStatus.Completed : ViewResultStatus.Dismissed;
            if (cleanup == CleanupStatus.Pending && failure != null && error != null && !ReferenceEquals(failure, error))
            {
                error = new AggregateException("View failure and cleanup diagnostic.", failure, error);
            }

            result.TrySetResult(new ViewResult<TResult>(resultStatus, value, reason, error ?? failure, cleanup));
        }

        internal override void PublishResultObservers() => result.PublishObservers();
    }
}
