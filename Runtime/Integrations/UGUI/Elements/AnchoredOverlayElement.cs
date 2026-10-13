using System;
using UnityEngine;

namespace MUI.UGUI
{
    public enum OverlayDismissReason
    {
        TargetUnavailable,
        OutsidePointer,
        Back
    }

    /// <summary>局部浮层适配器；Presenter 持有其投影，并路由返回及外部指针输入。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class AnchoredOverlayElement : Element
    {
        [SerializeField]
        private RectTransform content = null;
        [SerializeField]
        private OverlayPlacement placement = OverlayPlacement.Below;
        [SerializeField, Min(0)]
        private float gap = 8;
        [SerializeField, Min(0)]
        private float edgePadding = 8;
        [SerializeField]
        private bool dismissOutside = true;
        [SerializeField]
        private bool dismissOnBack = true;
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform bounds;
        private RectTransform anchor;
        private bool refreshing;
        private long version;

        public event Action<OverlayDismissReason> Dismissed;

        public OverlayPlacement ActualPlacement
        {
            get; private set;
        }

        public RectTransform ContentRoot
        {
            get
            {
                RequireAlive();
                return content;
            }
        }

        public RectTransform Anchor
        {
            get
            {
                RequireAlive();
                return anchor;
            }

            set
            {
                Initialize();
                if (value == null)
                {
                    value = null;
                }

                if (ReferenceEquals(anchor, value))
                {
                    return;
                }

                if (value != null)
                {
                    ValidateAnchor(value);
                }

                anchor = value;
                var expectedVersion = ++version;
                content.gameObject.SetActive(false);
                if (!IsAlive || version != expectedVersion)
                {
                    return;
                }

                Refresh();
                if (IsAlive)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>输入适配可在占用本帧返回前检查；不关闭浮层或触发回调。</summary>
        public bool CanHandleBack => IsAlive && dismissOnBack && CanHandleInput();

        protected override void OnInitialize()
        {
            bounds = (RectTransform)transform;
            if (content == null || content.parent != bounds)
            {
                throw new InvalidOperationException("Overlay content must be a direct child of its bounds adapter.");
            }

            ValidateContent();
            OnDispose(() =>
            {
                anchor = null;
                ++version;
                Dismissed = null;
                if (content != null)
                {
                    content.gameObject.SetActive(false);
                }
            });
            content.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (IsAlive)
            {
                Refresh();
            }
        }

        private void OnEnable()
        {
            if (IsAlive)
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (bounds != null && content != null)
            {
                content.gameObject.SetActive(false);
            }
        }

        public void Refresh()
        {
            if (!IsAlive || !isActiveAndEnabled || bounds == null || content == null || refreshing)
            {
                return;
            }

            refreshing = true;
            try
            {
                var expectedVersion = version;
                if (anchor == null || !anchor.gameObject.activeInHierarchy)
                {
                    if (!ReferenceEquals(anchor, null))
                    {
                        Dismiss(OverlayDismissReason.TargetUnavailable);
                    }
                    else
                    {
                        content.gameObject.SetActive(false);
                    }

                    return;
                }

                try
                {
                    ValidateAnchor(anchor);
                }
                catch (InvalidOperationException)
                {
                    Dismiss(OverlayDismissReason.TargetUnavailable);
                    return;
                }

                ValidateContent();
                if (float.IsNaN(edgePadding) || float.IsInfinity(edgePadding) || edgePadding < 0)
                {
                    throw new InvalidOperationException("Overlay edge padding must be finite and nonnegative.");
                }

                anchor.GetWorldCorners(corners);
                var first = bounds.InverseTransformPoint(corners[0]);
                var min = new Vector2(first.x, first.y);
                var max = min;
                for (var i = 1; i < corners.Length; ++i)
                {
                    var point = bounds.InverseTransformPoint(corners[i]);
                    min = Vector2.Min(min, new Vector2(point.x, point.y));
                    max = Vector2.Max(max, new Vector2(point.x, point.y));
                }

                var area = bounds.rect;
                var insetX = Mathf.Min(edgePadding, area.width * 0.5f);
                var insetY = Mathf.Min(edgePadding, area.height * 0.5f);
                area = Rect.MinMaxRect(area.xMin + insetX, area.yMin + insetY, area.xMax - insetX, area.yMax - insetY);
                var rect = AnchoredOverlayPlacement.Calculate(Rect.MinMaxRect(min.x, min.y, max.x, max.y), content.rect.size, area, placement, gap, out var actual);
                ActualPlacement = actual;
                var pivot = new Vector3(rect.x + content.pivot.x * rect.width, rect.y + content.pivot.y * rect.height, 0);
                content.localPosition = pivot;
                if (IsAlive && version == expectedVersion && content != null && anchor != null)
                {
                    content.gameObject.SetActive(true);
                }
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>由宿主路由的指针按下事件调用，不轮询或全局拦截输入。</summary>
        public bool HandleOutsidePointer(Vector2 screenPosition, Camera eventCamera)
        {
            RequireAlive();
            if (!dismissOutside || !CanHandleInput())
            {
                return false;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(content, screenPosition, eventCamera))
            {
                return false;
            }

            if (anchor != null && RectTransformUtility.RectangleContainsScreenPoint(anchor, screenPosition, eventCamera))
            {
                return false;
            }

            Dismiss(OverlayDismissReason.OutsidePointer);
            return true;
        }

        /// <summary>关闭当前可交互浮层；由局部返回处理器或输入适配派发。</summary>
        public bool HandleBack()
        {
            RequireAlive();
            if (!CanHandleBack)
            {
                return false;
            }

            Dismiss(OverlayDismissReason.Back);
            return true;
        }

        private bool CanHandleInput()
        {
            if (!isActiveAndEnabled || content == null || !content.gameObject.activeInHierarchy || anchor == null)
            {
                return false;
            }

            var view = GetComponentInParent<View>(true);
            return view == null || view.IsInputEnabled;
        }

        private void Dismiss(OverlayDismissReason reason)
        {
            anchor = null;
            var expectedVersion = ++version;
            if (content != null)
            {
                content.gameObject.SetActive(false);
            }

            if (!IsAlive || version != expectedVersion)
            {
                return;
            }

            var handlers = Dismissed;
            if (handlers != null)
            {
                foreach (Action<OverlayDismissReason> handler in handlers.GetInvocationList())
                {
                    if (!IsAlive || version != expectedVersion)
                    {
                        break;
                    }

                    try
                    {
                        handler(reason);
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }

            if (IsAlive)
            {
                NotifyChanged(nameof(Anchor));
            }
        }

        private void ValidateAnchor(RectTransform value)
        {
            if (value == content || value.IsChildOf(content))
            {
                throw new InvalidOperationException("Overlay cannot be anchored to its own content subtree.");
            }

            var ownCanvas = GetComponentInParent<Canvas>();
            var targetCanvas = value.GetComponentInParent<Canvas>();
            if (ownCanvas == null || targetCanvas == null || ownCanvas.rootCanvas != targetCanvas.rootCanvas)
            {
                throw new InvalidOperationException("Overlay and anchor must belong to the same root Canvas.");
            }
        }

        private void ValidateContent()
        {
            if (content.localScale != Vector3.one || Quaternion.Angle(content.localRotation, Quaternion.identity) > 0.01f)
            {
                throw new InvalidOperationException("Overlay layout root requires unit scale and identity rotation; animate a child instead.");
            }
        }
    }
}
