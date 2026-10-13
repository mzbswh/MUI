using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    [GenerateControlProperty(nameof(Target), "interactable", "Interactable")]
    [GenerateControlEvent(nameof(Target), "onClick", "Clicked")]
    public sealed partial class ButtonElement : Element, IPointerDownHandler
    {
        private Button target;
        private PointerEventData pressedPointer;


        private Button Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Button>();
                }

                return target;
            }
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Button;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left && CanReceiveInput(target))
            {
                pressedPointer = eventData;
            }
        }

        // 只有弹窗指针捕获需要额外适配，事件订阅和清理由生成器完成。
        partial void OnClickedInvoking()
        {
            if (pressedPointer != null && pressedPointer.eligibleForClick && pressedPointer.pointerPress == gameObject)
            {
                var owner = GetComponentInParent<View>(true);
                if (owner != null) owner.CaptureModalPointer(pressedPointer);
            }
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<Button>();
            OnDispose(() => pressedPointer = null);
        }
    }
}
