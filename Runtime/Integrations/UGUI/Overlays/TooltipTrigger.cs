using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>对借用且已准备好的锚定提示框提供悬停和选中触发。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField]
        private AnchoredOverlayElement overlay = null;
        [SerializeField, Min(0)]
        private double displayDelay = 0.4;
        [SerializeField]
        private bool automaticClock = true;
        private readonly HashSet<int> pointers = new HashSet<int>();
        private RectTransform target;
        private View view;
        private Selectable selectable;
        private bool selected;
        private bool suppressed;
        private bool ownsDisplay;
        private bool subscribed;
        private double elapsed;

        private bool HasIntent => selected || pointers.Count != 0;

        private void OnEnable()
        {
            if (overlay == null)
            {
                throw new InvalidOperationException("TooltipTrigger requires an anchored overlay.");
            }

            ValidateDelay();
            target = (RectTransform)transform;
            BindView();
            selectable = GetComponent<Selectable>();
            overlay.Dismissed += OnDismissed;
            overlay.PropertyChanged += OnOverlayChanged;
            subscribed = true;
            // 在已选中控件上启用触发器时，不应要求再次发生导航事件。
            var eventSystem = EventSystem.current;
            selected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
        }

        private void OnDisable()
        {
            if (overlay != null && subscribed)
            {
                overlay.Dismissed -= OnDismissed;
                overlay.PropertyChanged -= OnOverlayChanged;
            }

            subscribed = false;
            UnbindView();
            pointers.Clear();
            selected = false;
            suppressed = false;
            elapsed = 0;
            ReleaseDisplay();
        }

        private void OnTransformParentChanged()
        {
            ReleaseDisplay();
            elapsed = 0;
            UnbindView();
            if (isActiveAndEnabled)
            {
                BindView();
            }
        }

        private void Update()
        {
            if (automaticClock)
            {
                Advance(Time.unscaledDeltaTime);
            }
        }

        /// <summary>使用手动时钟时，禁用 Prefab 的 Automatic Clock，并每帧调用一次。</summary>
        public void Advance(double unscaledDeltaTime)
        {
            if (double.IsNaN(unscaledDeltaTime) || double.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            ValidateDelay();
            if (!CanDisplay() || !HasIntent)
            {
                elapsed = 0;
                ReleaseDisplay();
                return;
            }

            if (suppressed || ownsDisplay)
            {
                return;
            }

            elapsed = unscaledDeltaTime >= displayDelay - elapsed ? displayDelay : elapsed + unscaledDeltaTime;
            if (elapsed < displayDelay)
            {
                return;
            }

            ownsDisplay = true;
            try
            {
                overlay.Anchor = target;
                if (!isActiveAndEnabled || overlay == null || !overlay.IsAlive || overlay.Anchor != target)
                {
                    ownsDisplay = false;
                    suppressed = true;
                }
            }
            catch
            {
                ownsDisplay = false;
                suppressed = true;
                throw;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || eventData == null || pointers.Count >= 16)
            {
                return;
            }

            var wasIdle = !HasIntent;
            pointers.Add(eventData.pointerId);
            if (wasIdle)
            {
                BeginIntent();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            pointers.Remove(eventData.pointerId);
            EndIntentIfIdle();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // 点击或触摸开始新的交互；悬停或选中状态离开前不要重新打开。
            suppressed = true;
            elapsed = 0;
            ReleaseDisplay();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            var wasIdle = !HasIntent;
            selected = true;
            if (wasIdle)
            {
                BeginIntent();
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            EndIntentIfIdle();
        }

        private void BeginIntent()
        {
            elapsed = 0;
            suppressed = false;
        }

        private void EndIntentIfIdle()
        {
            if (HasIntent)
            {
                return;
            }

            elapsed = 0;
            suppressed = false;
            ReleaseDisplay();
        }

        private bool CanDisplay() => overlay != null && overlay.IsAlive && overlay.isActiveAndEnabled && target != null && (view == null || view.IsInputEnabled) && (selectable == null || (selectable.isActiveAndEnabled && selectable.IsInteractable()));

        private void BindView()
        {
            view = GetComponentInParent<View>(true);
            if (view != null)
            {
                view.InputStateChanged += OnGateChanged;
            }
        }

        private void UnbindView()
        {
            if (view != null)
            {
                view.InputStateChanged -= OnGateChanged;
            }

            view = null;
        }

        private void OnGateChanged()
        {
            if (CanDisplay())
            {
                return;
            }

            elapsed = 0;
            ReleaseDisplay();
        }

        private void ReleaseDisplay()
        {
            if (!ownsDisplay)
            {
                return;
            }

            ownsDisplay = false;
            if (overlay != null && overlay.IsAlive && overlay.Anchor == target)
            {
                overlay.Anchor = null;
            }
        }

        private void OnDismissed(OverlayDismissReason reason)
        {
            if (!ownsDisplay)
            {
                return;
            }

            ownsDisplay = false;
            suppressed = true;
            elapsed = 0;
        }

        private void OnOverlayChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!ownsDisplay || (args.PropertyName != nameof(AnchoredOverlayElement.Anchor) && !string.IsNullOrEmpty(args.PropertyName)))
            {
                return;
            }

            if (overlay != null && overlay.IsAlive && overlay.Anchor == target)
            {
                return;
            }

            ownsDisplay = false;
            suppressed = true;
            elapsed = 0;
        }

        private void ValidateDelay()
        {
            if (double.IsNaN(displayDelay) || double.IsInfinity(displayDelay) || displayDelay < 0)
            {
                throw new InvalidOperationException("Tooltip display delay must be finite and nonnegative.");
            }
        }
    }
}
