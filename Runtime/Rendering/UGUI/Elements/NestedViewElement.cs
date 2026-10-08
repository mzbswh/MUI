using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>
    /// 父 Prefab 持有的静态子 View。赋值 ViewModel 会请求
    /// 生成绑定子视图；设为 null 会移除子视图并隐藏子界面。
    /// 模型为借用对象，应将此边界放在子 View 上方的包装节点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class NestedViewElement : Element, IElementBoundary, IChildViewElement, IBindingRebindTarget
    {
        [SerializeField]
        private View childView;
        private BorrowedViewProvider provider;
        private ChildViewTemplate<ViewModel, Unit> template;
        private ChildViewScope scope;
        private LifetimeScope parentLifetime;
        private ChildViewHandle<ViewModel, Unit> childHandle;
        private ViewModel requestedModel;
        private long version;
        private bool preparing;
        private Task pendingChange;
        private PreparedRebind preparedRebind;

        public ViewModel ViewModel
        {
            get => requestedModel;
            set
            {
                RequireAlive();
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

        Task IChildViewElement.Preparation => (pendingChange ?? Task.CompletedTask);

        protected override void OnInitialize()
        {
            if (childView == null)
            {
                foreach (Transform child in transform)
                {
                    var candidate = child.GetComponent<View>();
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (childView != null)
                    {
                        throw new InvalidOperationException("NestedViewElement requires one explicit child View when several are present.");
                    }

                    childView = candidate;
                }
            }

            if (childView == null || childView.transform == transform || !childView.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException("NestedViewElement requires a descendant View inside its wrapper.");
            }

            if (GetComponent<View>() != null)
            {
                throw new InvalidOperationException("Place NestedViewElement on a wrapper, not on a View root.");
            }

            var resource = new ViewResource("nested:" + GetInstanceID());
            provider = new BorrowedViewProvider(resource, childView);
            template = new ChildViewTemplate<ViewModel, Unit>(resource, () => throw new InvalidOperationException("Nested child views require an assigned model."), BindingRegistry.Create);
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

        void IChildViewElement.BeginParentActivation(ChildViewScope parentScope, LifetimeScope lifetime)
        {
            RequireAlive();

            ++version;
            scope = parentScope;
            parentLifetime = lifetime;
            // 根据 View 契约，前一次激活必须已经完全排空。
            childHandle = null;
            requestedModel = null;
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
            var previousModel = DisplayedViewModel;
            var precedingChange = pendingChange;
            // 子命令内发起的替换不能等待自身换绑，仍走关闭后重新准备。
            var rebind = model != null && previous != null && previous.IsActive && previous.CanRebind;
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
                    ChangeAsync(previous, previousModel, precedingChange, model, targetScope, request, rebind, token)).AsTask(), completion);
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

        private async ValueTask<bool> ChangeAsync(ChildViewHandle<ViewModel, Unit> previous,
                    ViewModel previousModel,
                    Task precedingChange,
                    ViewModel model,
                    ChildViewScope targetScope,
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

            if (rebind && previous.IsActive && previous.CanRebind)
            {
                RebindOutcome outcome;
                try
                {
                    preparing = true;
                    // 子句柄在旧订阅仍有效时准备候选；失败时保留当前画面。
                    outcome = await previous.RebindAsync(model, token);
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

            ChildViewHandle<ViewModel, Unit> candidate = null;
            Exception failure;
            try
            {
                preparing = true;
                candidate = await targetScope.PrepareAsync(template, provider, Unit.Value, model, token);
                token.ThrowIfCancellationRequested();
                if (!IsAlive || request != version || !ReferenceEquals(scope, targetScope) || !targetScope.IsActive)
                {
                    throw new OperationCanceledException(token);
                }

                candidate.Commit();
                childHandle = candidate;
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
            else if (failure is ChildViewPreparationException preparation)
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
                ChildViewHandle<ViewModel, Unit> restored = null;
                try
                {
                    preparing = true;
                    restored = await targetScope.PrepareAsync(template, provider, Unit.Value, previousModel, token);
                    token.ThrowIfCancellationRequested();
                    if (!IsAlive || request != version || !ReferenceEquals(scope, targetScope) || !targetScope.IsActive)
                    {
                        throw new OperationCanceledException(token);
                    }

                    restored.Commit();
                    childHandle = restored;
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
                        else if (restoration is ChildViewPreparationException failedRestore)
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
