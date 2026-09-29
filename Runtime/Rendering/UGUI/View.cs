using System;
using System.Collections.Generic;
using System.Threading;
using MUI.ChildViews;
using UnityEngine;

namespace MUI.UGUI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed partial class View : MonoBehaviour, IOrderedView, IChildViewHost, IModalView, IInputView, IFocusView, IVisibilityView, IChildTickHost, IChildRequestHost, IEnterTransitionView, IExitTransitionView, IDisposable
    {
        private InputGate inputGate;
        private bool lastInputEnabled;
        private readonly List<Element> elements = new List<Element>();
        private ElementIndex index;
        private CanvasGroup group;
        private bool disposed;
        private bool hostVisible;
        private bool hostInteractable;
        private bool localVisible = true;
        private bool localInteractable = true;
        private ChildViewScope childViews;
        private CancellationToken childActivationToken;
        private bool configuringCreation;

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
                return this != null && !disposed && (index == null || group != null);
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
            if (configuringCreation || childViews != null)
            {
                throw new InvalidOperationException("创建配置必须在 View 首次激活之前执行，且不能重入。");
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

        public void BeginChildActivation(Lifetime activation)
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

            if (childViews != null && !childViews.IsDisposedSuccessfully)
            {
                throw new InvalidOperationException("Previous child activation has not finished cleanup.");
            }

            // 在改变激活状态前验证模式；控件仅借用配置，收到 Source 写入时才取得资源。
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
            try
            {
                Collect(transform, candidate, true);
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
        }

        private void Collect(Transform node, ElementIndex candidate, bool isRoot)
        {
            if (!isRoot && node.GetComponent<View>() != null)
            {
                return;
            }

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
        }

        private void RequireAlive()
        {
            if (!IsAlive)
            {
                throw new ObjectDisposedException(nameof(View));
            }
        }

        public void Dispose()
        {
            UnityMainThread.Require();
            if (disposed)
            {
                return;
            }

            disposed = true;
            synchronousResourceLoader = null;
            activeResourceContext = null;
            resourceLoader = null;
            var errors = new List<Exception>();
            try
            {
                ClearLocalBackHandlers();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                EndVisualRetention();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            if (inputGate != null)
            {
                try
                {
                    inputGate.Changed -= ApplyGates;
                    inputGate.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            try
            {
                ReleaseModalBarrier();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            if (childViews != null)
            {
                try
                {
                    childViews.Cancel();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
                finally
                {
                    childViews.TickActivityChanged -= NotifyChildTicks;
                }
            }

            ChildTickActivityChanged = null;
            try
            {
                ApplyGates();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            for (var i = elements.Count - 1; i >= 0; --i)
            {
                var element = elements[i];
                if (element == null)
                {
                    continue;
                }

                try
                {
                    element.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            InputStateChanged = null;
            previousSelection = null;
            elements.Clear();
            index = null;
            if (errors.Count > 0)
            {
                throw new AggregateException("View cleanup failed.", errors);
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
            try
            {
                Dispose();
            }
            catch (Exception error)
            {
                UnityErrorLogging.Report(error);
            }
        }
    }
}
