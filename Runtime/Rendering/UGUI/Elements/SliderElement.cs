using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Slider))]
    [GenerateControlProperty(nameof(Target), "interactable", "Interactable")]
    [GenerateControlProperty(nameof(Target), "value", "Value", Setter = "SetValueWithoutNotify")]
    [GenerateControlEvent(nameof(Target), "onValueChanged", NotifyProperty = "Value")]
    public sealed partial class SliderElement : Element, IUIAutomationInput<float>
    {
        private Slider target;

        private Slider Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Slider>();
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
