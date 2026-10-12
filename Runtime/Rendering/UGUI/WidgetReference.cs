using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MUI.Widgets;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>父 View 持有的静态 Widget 绑定目标，不创建节点或组件。</summary>
    public sealed partial class WidgetReference : IElement, IWidgetElement, IBindingRebindTarget, IDisposable
    {
        private readonly View childView;
        private readonly Func<bool> ownerAlive;
        private bool disposed;
        private Action finalCleanup;
        private object args;
        private object displayedArgs;
        private readonly Func<WidgetScope, IViewProvider, ViewModel, object, CancellationToken, ValueTask<WidgetHandle>> prepare;
        private IViewProvider provider;
        private readonly List<CleanupResponsibility> childViewReleases = new List<CleanupResponsibility>();
        private WidgetScope scope;
        private LifetimeScope parentLifetime;
        private WidgetHandle childHandle;
        private ViewModel requestedModel;
        private long version;
        private bool preparing;
        private Task pendingChange;
        private PreparedRebind preparedRebind;

        public WidgetReference(string name, View childView, Func<bool> ownerAlive,
            Func<WidgetScope, IViewProvider, ViewModel, object, CancellationToken, ValueTask<WidgetHandle>> prepare = null)
        {
            Name = name;
            this.childView = childView;
            this.ownerAlive = ownerAlive ?? throw new ArgumentNullException(nameof(ownerAlive));
            this.prepare = prepare ?? ((scope, provider, vm, args, token) => WidgetRegistry.PrepareAsync(scope, provider, new ViewResource("nested:" + childView.GetInstanceID()), vm, args, token));
            if (childView == null)
                throw new ArgumentNullException(nameof(childView));
            try
            {
                Initialize();
            }
            catch { childView.InputStateChanged -= OnChildStateChanged; throw; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name
        {
            get;
        }

        public bool IsAlive => !disposed && ownerAlive() && childView != null && childView.IsAlive;

        public ViewModel VM
        {
            get => ViewModel; set => ViewModel = value;
        }

        public object Args
        {
            get => args;
            set
            {
                RequireAlive();
                if (Equals(args, value) && (pendingChange == null || !pendingChange.IsCompleted ||
                    (pendingChange.Status == TaskStatus.RanToCompletion && Equals(displayedArgs, value))))
                    return;
                if (preparing || preparedRebind != null)
                    throw new InvalidOperationException("Cannot change Widget args during preparation or rebind commit.");
                args = value;
                RequestChange();
                NotifyChanged();
            }
        }

        public ViewModel ViewModel
        {
            get => requestedModel;
            set
            {
                RequireAlive();
                RequireAvailableNode();
                if (preparing)
                {
                    throw new InvalidOperationException("Cannot change a nested child view while preparing its bindings.");
                }

                if (preparedRebind != null)
                {
                    preparedRebind.Write(value);
                    return;
                }

                if (ReferenceEquals(value, requestedModel) && ((childHandle != null && childHandle.IsActive && ReferenceEquals(childHandle.Model, value)) || (pendingChange != null && !pendingChange.IsCompleted)))
                {
                    return;
                }

                if (value != null && scope != null && scope.IsActive)
                {
                    BindingRegistry.GetManifest(value.GetType());
                }

                requestedModel = value;
                RequestChange();
                NotifyChanged();
                NotifyChanged(nameof(VM));
            }
        }

        /// <summary>包含前一次命令清理及最新请求的绑定。</summary>
        public Task PendingChange
        {
            get
            {
                if (IsExecutingChild)
                {
                    throw new InvalidOperationException("A child command cannot await replacement of its own child view.");
                }

                return (pendingChange ?? Task.CompletedTask);
            }
        }

        /// <summary>当前活动子视图实际使用的模型；准备失败时仍为原模型。</summary>
        public ViewModel DisplayedViewModel => childHandle != null && childHandle.IsActive ? childHandle.Model : null;

        /// <summary>当前调用链是否来自此子项，供列表延后同步回收并拒绝业务等待自身清理。</summary>
        internal bool IsExecutingChild => childHandle != null && childHandle.IsExecuting;

        Task IWidgetElement.Preparation => (pendingChange ?? Task.CompletedTask);

        private void RequireAlive()
        {
            if (!IsAlive)
                throw new ObjectDisposedException(Name);
        }

        private void RequireAvailableNode() => RequireAlive();

        private void NotifyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void OnDispose(Action action) => finalCleanup += action;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            childView.InputStateChanged -= OnChildStateChanged;
            try
            {
                finalCleanup?.Invoke();
            }
            finally { finalCleanup = null; PropertyChanged = null; }
        }

        private void OnChildStateChanged()
        {
            if (childView != null && childView.IsAlive)
                return;
            if (childHandle != null && childHandle.IsActive)
                childHandle.RequestClose();
        }

        internal Func<bool> CaptureWidgetCleanupConfirmation()
        {
            childViewReleases.RemoveAll(release => release.CaptureSnapshot().State == CleanupResponsibilityState.Completed);
            var releases = childViewReleases.ToArray();
            return () => Array.TrueForAll(releases, release => release.CaptureSnapshot().State == CleanupResponsibilityState.Completed);
        }

        private void Initialize()
        {
            childView.InputStateChanged += OnChildStateChanged;
            var resource = new ViewResource("nested:" + childView.GetInstanceID());
            var borrowed = new BorrowedViewProvider(resource, childView);
            provider = new DelegateViewProvider(async (requested, token) =>
            {
                RequireAvailableNode();
                var acquired = await borrowed.AcquireAsync(requested, token);
                // 借用节点仍有独占持有者时不能销毁；凭证只在子激活及实例责任确认后归还。
                childViewReleases.RemoveAll(release => release.CaptureSnapshot().State == CleanupResponsibilityState.Completed);
                childViewReleases.Add(CleanupRegistry.GetResponsibility(acquired, "NestedViewAcquisition"));
                return acquired;
            });
            childView.SetHostState(false, false);
            OnDispose(() =>
            {
                ++version;
                try
                {
                    if (childHandle != null && childHandle.IsActive)
                    {
                        childHandle.RequestClose();
                    }
                }
                finally
                {
                    ClearParentReferences();
                }
            });
        }

        void IWidgetElement.BeginParentActivation(WidgetScope parentScope, LifetimeScope lifetime)
        {
            RequireAlive();
            RequireAvailableNode();

            ++version;
            scope = parentScope;
            parentLifetime = lifetime;
            // 根据 View 契约，前一次激活必须已经完全排空。
            childHandle = null;
            requestedModel = null;
            args = null;
            displayedArgs = null;
            pendingChange = null;
            preparedRebind = null;
            lifetime.OnDispose(() =>
            {
                // LifetimeScope 已排空本次换绑操作；Scope 继续持有并负责关闭子句柄。
                // 只解除控件的托管引用，不修改原生画面，也不提前归还视图资源。
                if (ReferenceEquals(parentLifetime, lifetime))
                {
                    ++version;
                    ClearParentReferences();
                }
            });
            // 父绑定提供本次激活的模型，不应短暂绑定
            // 上一个父模型留下且可能已释放的模型。
        }

        private void ClearParentReferences()
        {
            scope = null;
            parentLifetime = null;
            childHandle = null;
            requestedModel = null;
            args = null;
            displayedArgs = null;
            pendingChange = null;
            preparedRebind = null;
        }

        private void RequestChange()
        {
            var request = ++version;
            var targetScope = scope;
            var targetLifetime = parentLifetime;
            var model = requestedModel;
            if (targetScope == null || !targetScope.IsActive)
            {
                return;
            }

            var previous = childHandle;
            var requestedArgs = args;
            var priorArgs = displayedArgs;
            var previousModel = DisplayedViewModel;
            var precedingChange = pendingChange;
            // 子命令内发起的替换不能等待自身换绑，仍走关闭后重新准备。
            var rebind = model != null && previous != null && previous.IsActive && previous.CanRebindModel && previous.Model.GetType() == model.GetType();
            // 关闭及绑定会调用项目代码；先发布本次任务，重入读取不能观察旧请求。
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var change = completion.Task;
            pendingChange = change;
            try
            {
                // 不等待的关闭支持旧子界面命令发起的赋值。
                if (!rebind && previous != null && previous.IsActive)
                {
                    previous.RequestClose();
                }

                _ = CompleteChangeAsync(targetLifetime.RunAsync(token =>
                    ChangeAsync(previous, previousModel, precedingChange, model, requestedArgs, priorArgs, targetScope, request, rebind, token)).AsTask(), completion);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }

            if (request != version || !ReferenceEquals(scope, targetScope))
            {
                _ = ObserveAsync(change);
                return;
            }

            if (change.IsCompleted)
            {
                // 初始绑定失败参与父级准备阶段的回滚。
                change.GetAwaiter().GetResult();
            }
            else
            {
                _ = ObserveAsync(change);
            }
        }

        private static async Task CompleteChangeAsync(Task<bool> operation, TaskCompletionSource<bool> completion)
        {
            try
            {
                completion.TrySetResult(await operation);
            }
            catch (OperationCanceledException error)
            {
                completion.TrySetCanceled(error.CancellationToken);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }

        private async ValueTask<bool> ChangeAsync(WidgetHandle previous,
                    ViewModel previousModel,
                    Task precedingChange,
                    ViewModel model,
                    object requestedArgs,
                    object priorArgs,
                    WidgetScope targetScope,
                    long request,
                    bool rebind,
                    CancellationToken token)
        {
            // 再次借用同一个原生节点前，先完成前次请求的物理
            // 回滚；该回滚失败归前次请求调用者处理。
            try
            {
                if (precedingChange != null)
                {
                    await precedingChange;
                }
            }
            catch (Exception)
            {
            }

            token.ThrowIfCancellationRequested();
            if (!IsAlive || request != version || !ReferenceEquals(scope, targetScope) || !targetScope.IsActive)
            {
                return false;
            }

            if (rebind && previous.IsActive && ReferenceEquals(previous.Model, model) && !Equals(priorArgs, requestedArgs))
            {
                var update = await previous.UpdateWidgetArgsAsync(requestedArgs, token);
                if (update.Error != null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(update.Error).Throw();
                if (!update.IsApplied)
                    throw new InvalidOperationException("Widget args update rejected: " + update.Rejection);
                if (request == version)
                    displayedArgs = requestedArgs;
                return true;
            }
            if (rebind && previous.IsActive && previous.CanRebindModel)
            {
                RebindOutcome outcome;
                try
                {
                    preparing = true;
                    // 子句柄在旧订阅仍有效时准备候选；失败时保留当前画面。
                    outcome = await previous.RebindModelAsync(model, token);
                }
                finally
                {
                    preparing = false;
                }

                token.ThrowIfCancellationRequested();
                if (!IsAlive || request != version || !ReferenceEquals(scope, targetScope) || !targetScope.IsActive)
                {
                    return false;
                }

                if (outcome.IsApplied)
                {
                    if (outcome.Error != null)
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outcome.Error).Throw();
                    }

                    if (!Equals(priorArgs, requestedArgs))
                    {
                        var update = await previous.UpdateWidgetArgsAsync(requestedArgs, token);
                        if (update.Error != null)
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(update.Error).Throw();
                        if (!update.IsApplied)
                            throw new InvalidOperationException("Widget args update rejected: " + update.Rejection);
                        displayedArgs = requestedArgs;
                    }
                    return true;
                }

                if (outcome.Status != RebindStatus.Rejected)
                {
                    if (outcome.Error != null)
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outcome.Error).Throw();
                    }

                    if (outcome.Status == RebindStatus.Cancelled)
                    {
                        throw new OperationCanceledException("Nested child rebind was cancelled.", token);
                    }

                    throw new InvalidOperationException("Nested child rebind failed without an error.");
                }
            }

            if (rebind && previous.IsActive)
            {
                previous.RequestClose();
            }

            if (previous != null)
            {
                await previous.CleanupCompletion;
            }

            token.ThrowIfCancellationRequested();
            if (!IsAlive || request != version || !ReferenceEquals(scope, targetScope) || !targetScope.IsActive)
            {
                return false;
            }

            childHandle = null;
            if (model == null)
            {
                return true;
            }

            WidgetHandle candidate = null;
            Exception failure;
            try
            {
                preparing = true;
                candidate = await prepare(targetScope, provider, model, requestedArgs, token);
                token.ThrowIfCancellationRequested();
                if (!IsAlive || request != version || !ReferenceEquals(scope, targetScope) || !targetScope.IsActive)
                {
                    throw new OperationCanceledException(token);
                }

                candidate.Commit();
                childHandle = candidate;
                displayedArgs = requestedArgs;
                return true;
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                preparing = false;
            }

            if (candidate != null)
            {
                try
                {
                    await candidate.DisposeAsync();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Nested binding and cleanup failed.", failure, cleanup);
                }
            }
            else if (failure is WidgetPreparationException preparation)
            {
                try
                {
                    await preparation.CleanupCompletion;
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Nested preparation and cleanup failed.", failure, cleanup);
                }
            }

            if (previousModel != null && !ReferenceEquals(previousModel, model) && IsAlive && !token.IsCancellationRequested && request == version && targetScope.IsActive)
            {
                WidgetHandle restored = null;
                try
                {
                    preparing = true;
                    restored = await prepare(targetScope, provider, previousModel, priorArgs, token);
                    token.ThrowIfCancellationRequested();
                    if (!IsAlive || request != version || !ReferenceEquals(scope, targetScope) || !targetScope.IsActive)
                    {
                        throw new OperationCanceledException(token);
                    }

                    restored.Commit();
                    childHandle = restored;
                    displayedArgs = priorArgs;
                }
                catch (Exception restoration)
                {
                    preparing = false;
                    try
                    {
                        if (restored != null)
                        {
                            await restored.DisposeAsync();
                        }
                        else if (restoration is WidgetPreparationException failedRestore)
                        {
                            await failedRestore.CleanupCompletion;
                        }
                    }
                    catch (Exception cleanup)
                    {
                        throw new AggregateException(failure, restoration, cleanup);
                    }

                    throw new AggregateException("Nested replacement and restoration failed.", failure, restoration);
                }
                finally
                {
                    preparing = false;
                }
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return false;
        }

        private static async Task ObserveAsync(Task change)
        {
            try
            {
                await change;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
