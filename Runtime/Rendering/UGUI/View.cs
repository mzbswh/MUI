using System;
using System.Collections.Generic;
using System.Threading;
using MUI.ChildViews;
using UnityEngine;

namespace MUI.UGUI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed partial class View : MonoBehaviour, IOrderedView, IChildViewHost, IModalView, IInputGestureView, IFocusView, IVisibilityView, IChildTickHost, IEnterTransitionView, IExitTransitionView, ICacheableView, IDisposable, IAsyncDisposable, ICleanupResponsibilitySource
    {
        private InputGate inputGate;
        private bool lastInputEnabled;
        private readonly List<Element> elements = new List<Element>();
        private ElementIndex index;
        private CanvasGroup group;
        private bool disposed;
        private bool nativeDestroyed;
        private bool hostVisible;
        private bool hostInteractable;
        private bool localVisible = true;
        private bool localInteractable = true;
        private ChildViewScope childViews;
        private LifetimeScope activeActivation;
        private CancellationToken childActivationToken;
        private bool configuringCreation;
        private bool initializing;

        public event Action InputStateChanged;

        public InputGate InputGate
        {
            get
            {
                RequireAlive();
                return inputGate ?? (inputGate = new InputGate());
            }
        }

        public bool IsVisible => IsAlive && gameObject.activeInHierarchy && (retainingVisuals ? retainedVisible && exitVisible : hostVisible && localVisible);

        public bool IsInputEnabled => !retainingVisuals && IsVisible && hostInteractable && localInteractable && (inputGate == null || inputGate.IsOpen);

        public bool IsAlive
        {
            get
            {
                UnityMainThread.Require();
                return this != null && !disposed && !nativeDestroyed && (index == null || group != null);
            }
        }

        /// <summary>子项共享此 View 的激活周期和有效渲染门控。</summary>
        public ChildViewScope ChildViews
        {
            get
            {
                RequireAlive();
                if (childViews == null || !childViews.IsActive)
                {
                    throw new InvalidOperationException("View has no active child childView scope.");
                }

                return childViews;
            }
        }

        public bool Visible
        {
            get => localVisible;
            set
            {
                RequireAlive();
                localVisible = value;
                ApplyGates();
            }
        }

        public bool Interactable
        {
            get => localInteractable;
            set
            {
                RequireAlive();
                localInteractable = value;
                ApplyGates();
            }
        }

        /// <summary>工厂配置只允许设置实例参数，禁止在回调中提前建立激活所有权。</summary>
        internal void ConfigureBeforeActivation(Action<View> configure)
        {
            RequireAlive();
            if (configuringCreation || (childViews != null && !childViews.IsCleanupConfirmed))
            {
                throw new InvalidOperationException("View 配置必须在首次激活前或旧激活完全清理后执行，且不能重入。");
            }

            configuringCreation = true;
            try
            {
                configure(this);
            }
            finally
            {
                configuringCreation = false;
            }
        }

        public void BeginChildActivation(LifetimeScope activation)
        {
            if (activation == null)
            {
                throw new ArgumentNullException(nameof(activation));
            }

            RequireAlive();
            if (configuringCreation)
            {
                throw new InvalidOperationException("不能在创建配置回调中激活 View。");
            }

            Initialize();
            if (retainingVisuals || releasingVisuals)
            {
                throw new InvalidOperationException("End visual retention before starting a new activation.");
            }

            if (childViews != null && !childViews.IsCleanupConfirmed)
            {
                throw new InvalidOperationException("Previous child activation has not finished cleanup.");
            }

            // 在改变激活状态前验证模式；控件仅借用配置，收到 Source 写入时才取得资源。
            InvalidateInputGestures();
            ResetFocusState();
            activeActivation = activation;
            var resourceContext = CreateResourceContext(activation);
            BeginResourceContext(activation, resourceContext);

            if (childViews != null)
            {
                childViews.TickActivityChanged -= NotifyChildTicks;
            }

            childViews = new ChildViewScope(activation);
            childActivationToken = activation.Token;
            childViews.TickActivityChanged += NotifyChildTicks;
            foreach (var element in elements)
            {
                if (element != null && element is GraphicElement graphic)
                {
                    graphic.BeginResourceActivation(resourceContext);
                }
            }

            ApplyGates();
            foreach (var element in elements)
            {
                if (element != null && element is IChildViewElement child)
                {
                    child.BeginParentActivation(childViews, activation);
                }
            }
        }

        public void Initialize()
        {
            UnityMainThread.Require();
            RequireAlive();
            if (index != null)
            {
                return;
            }
            if (initializing)
            {
                throw new InvalidOperationException("Cannot initialize a View from its own initialization callback.");
            }

            initializing = true;
            try
            {
                if (inputGate == null)
                {
                    inputGate = new InputGate();
                }

                inputGate.Changed -= ApplyGates;
                inputGate.Changed += ApplyGates;
                group = GetComponent<CanvasGroup>();
                if (group == null)
                {
                    throw new InvalidOperationException("View requires a CanvasGroup.");
                }

                ApplyGates();
                var candidate = new ElementIndex();
                Collect(transform, candidate, true);
                foreach (var selectable in GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                {
                    if (selectable.GetComponentInParent<View>(true) == this)
                    {
                        NativeFocusObserver.Attach(selectable.gameObject);
                    }
                }

                RequireAlive();
                index = candidate;
            }
            catch (Exception failure)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(failure, cleanupFailure);
                }

                throw;
            }
            finally
            {
                initializing = false;
            }
        }

        private void Collect(Transform node, ElementIndex candidate, bool isRoot)
        {
            if (!isRoot && node.GetComponent<View>() != null)
            {
                return;
            }

            AttachNativeGesture(node);

            var boundary = false;
            foreach (var behaviour in node.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour is IElementBoundary)
                {
                    boundary = true;
                }

                if (!(behaviour is Element element))
                {
                    continue;
                }

                elements.Add(element);
                GetCleanupScope().Own(element);
                element.Initialize();
                candidate.Add(element);
            }

            if (boundary)
            {
                return;
            }

            for (var i = 0; i < node.childCount; ++i)
            {
                Collect(node.GetChild(i), candidate, false);
            }
        }

        public TElement GetElement<TElement>(string elementName)
                    where TElement : class, IElement
        {
            Initialize();
            return index.Get<TElement>(elementName);
        }

        public void MoveToFront()
        {
            RequireAlive();
            transform.SetAsLastSibling();
            ModalPointerBarrier.RaiseHeldBarriers(transform.parent);
        }

        public void SetHostState(bool visible, bool interactable)
        {
            RequireAlive();
            hostVisible = visible;
            hostInteractable = interactable;
            Initialize();
            ApplyGates();
        }

        private void ApplyGates()
        {
            if (group == null)
            {
                return;
            }

            var visible = !disposed && (retainingVisuals ? retainedVisible && exitVisible : hostVisible && localVisible);
            var enabled = !retainingVisuals && visible && hostInteractable && localInteractable && (inputGate == null || inputGate.IsOpen);
            if (!enabled && lastInputEnabled)
            {
                // 先结束原生拖动，再改变 CanvasGroup；InputField 的收尾要求原生交互资格仍有效。
                InvalidateInputGestures();
            }
            group.alpha = visible ? (retainingVisuals ? retainedAlpha * exitAlpha : enterAlpha) : 0f;
            group.interactable = enabled;
            // 模态阻挡层是宿主持有的独立 Graphic，不是当前组。
            group.blocksRaycasts = enabled;
            if (!retainingVisuals && childViews != null && childViews.IsActive)
            {
                childViews.SetHostState(visible, enabled);
            }

            var effective = IsInputEnabled;
            if (lastInputEnabled != effective)
            {
                lastInputEnabled = effective;
                NotifyInputStateChanged();
            }
        }

        private void NotifyInputStateChanged()
        {
            var handlers = InputStateChanged;
            if (handlers != null)
            {
                foreach (Action handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler();
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }
        }

        private void RequireAlive()
        {
            if (!IsAlive)
            {
                throw new ObjectDisposedException(nameof(View));
            }
        }

        private void Awake() => Initialize();

        private void OnEnable()
        {
            if (index != null)
            {
                ApplyGates();
            }
        }

        private void OnDisable()
        {
            if (index != null && !disposed)
            {
                ApplyGates();
            }
        }

        private void OnDestroy()
        {
            nativeDestroyed = true;
            if (cleanupResponsibility != null &&
                cleanupResponsibility.CaptureSnapshot().State == CleanupResponsibilityState.Completed)
            {
                return;
            }
            // 先让拥有者撤销绑定与输入身份，再启动最终责任，避免关闭回调等待正在通知的同一责任。
            NotifyInputStateChanged();
            _ = ObserveNativeCleanupAsync(DisposeAsync());
        }
    }
}
