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
    public sealed partial class NestedViewElement : Element, IElementBoundary, IChildViewElement
    {
        [SerializeField]
        private View childView;
        private BorrowedViewProvider provider;
        private ChildViewTemplate<ViewModel, Unit> template;
        private ChildViewScope scope;
        private Lifetime parentLifetime;
        private ChildViewHandle childHandle;
        private ViewModel requestedModel;
        private ViewModel displayedModel;
        private long version;
        private bool preparing;
        private Task pendingChange;

        public ViewModel ViewModel
        {
            get => requestedModel;
            set
            {
                RequireAlive();
                if (preparing)
                {
                    throw new InvalidOperationException("Cannot reenter a nested childHandle while preparing its bindings.");
                }

                if (ReferenceEquals(value, requestedModel) && ((childHandle != null && childHandle.IsActive && ReferenceEquals(displayedModel, value)) || (!synchronousChange && pendingChange != null && !pendingChange.IsCompleted)))
                {
                    return;
                }

                if (value != null && scope != null && scope.IsActive)
                {
                    BindingRegistry.GetManifest(value.GetType());
                }

                if (scope != null && scope.Mode == LifetimeMode.Synchronous && childHandle != null
                    && (childHandle.IsExecuting || !childHandle.CanCloseSynchronously))
                {
                    throw new InvalidOperationException("Cannot synchronously replace a nested child while its command or lifecycle is executing.");
                }

                if (scope != null && scope.Mode == LifetimeMode.Synchronous && synchronousCleanupFailed)
                {
                    throw new InvalidOperationException("Nested view cleanup failed; this native view cannot be reused in the current activation.");
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
                    throw new InvalidOperationException("A child command cannot await replacement of its own childHandle.");
                }

                return GetPendingChange();
            }
        }

        public ViewModel DisplayedViewModel => childHandle != null && childHandle.IsActive ? displayedModel : null;

        /// <summary>当前调用链是否来自此子项，供列表延后同步回收并拒绝业务等待自身清理。</summary>
        internal bool IsExecutingChild => childHandle != null && childHandle.IsExecuting;

        Task IChildViewElement.Preparation => GetPendingChange();

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
            template = new ChildViewTemplate<ViewModel, Unit>(resource, () => throw new InvalidOperationException("Nested childViews require an assigned model."), BindingRegistry.Create, supportsSynchronousLifecycle: true);
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

        void IChildViewElement.BeginParentActivation(ChildViewScope parentScope, Lifetime lifetime)
        {
            RequireAlive();
            ++version;
            scope = parentScope;
            parentLifetime = lifetime;
            // 根据 View 契约，前一次激活必须已经完全排空。
            childHandle = null;
            requestedModel = null;
            displayedModel = null;
            pendingChange = null;
            synchronousCleanupFailed = false;
            synchronousPreparationFailure = null;
            synchronousChange = lifetime.Mode == LifetimeMode.Synchronous;
            synchronousCompletion = null;
            lifetime.OnDispose(() =>
            {
                // Lifetime 已排空本次换绑操作；Scope 继续持有并负责关闭子句柄。
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
            displayedModel = null;
            pendingChange = null;
            // 直接销毁可能发生在同步准备回调中；已交给调用方的信号仍由准备的 finally 完成。
            if (!preparing)
            {
                synchronousCompletion = null;
            }

            synchronousPreparationFailure = null;
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

            if (targetLifetime.Mode == LifetimeMode.Synchronous)
            {
                RequestSynchronousChange(model, targetScope, targetLifetime, request);
                return;
            }

            synchronousChange = false;
            var previous = childHandle;
            var previousModel = displayedModel;
            var precedingChange = pendingChange;
            // 不等待的关闭支持旧子界面命令发起的赋值。
            if (previous != null && previous.IsActive)
            {
                previous.RequestClose();
            }

            pendingChange = targetLifetime.RunAsync(token => ChangeAsync(previous, previousModel, precedingChange, model, targetScope, request, token)).AsTask();
            if (pendingChange.IsCompleted)
            {
                // 初始绑定失败参与父级准备阶段的回滚。
                pendingChange.GetAwaiter().GetResult();
            }
            else
            {
                _ = ObserveAsync(pendingChange);
            }
        }

        private async ValueTask<bool> ChangeAsync(ChildViewHandle previous,
                    ViewModel previousModel,
                    Task precedingChange,
                    ViewModel model,
                    ChildViewScope targetScope,
                    long request,
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
            displayedModel = null;
            if (model == null)
            {
                return true;
            }

            ChildViewHandle candidate = null;
            Exception failure;
            try
            {
                preparing = true;
                candidate = targetScope.Prepare(template, provider, Unit.Value, model);
                token.ThrowIfCancellationRequested();
                if (!IsAlive || request != version || !targetScope.IsActive)
                {
                    throw new OperationCanceledException(token);
                }

                candidate.Commit();
                childHandle = candidate;
                displayedModel = model;
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
                ChildViewHandle restored = null;
                try
                {
                    preparing = true;
                    restored = targetScope.Prepare(template, provider, Unit.Value, previousModel);
                    restored.Commit();
                    childHandle = restored;
                    displayedModel = previousModel;
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
