using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>独立滚动条适配，原生滚动事件受 View 输入门控约束。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Scrollbar))]
    [GenerateControlProperty(nameof(Target), "interactable", "Interactable")]
    [GenerateControlProperty(nameof(Target), "value", "Value", Setter = "SetValueWithoutNotify")]
    [GenerateControlEvent(nameof(Target), "onValueChanged", NotifyProperty = "Value")]
    public sealed partial class ScrollbarElement : Element, IUIAutomationInput<float>
    {
        private Scrollbar target;

        private Scrollbar Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Scrollbar>();
                }

                return target;
            }
        }

        UnityEngine.UI.Selectable IUIAutomationInput<float>.InputControl => Target;

        partial void OnValueChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new System.ArgumentOutOfRangeException(nameof(value), "控件数值必须为有限值。");
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Slider;

        bool IUIAutomationInput<float>.TrySetInput(float value)
        {
            var control = Target;
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new System.ArgumentOutOfRangeException(nameof(value));
            }

            control.value = value;
            return true;
        }
    }
}
