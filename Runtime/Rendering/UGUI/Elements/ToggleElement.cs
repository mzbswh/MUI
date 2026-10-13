using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Toggle))]
    [GenerateControlProperty(nameof(Target), "interactable", "Interactable")]
    [GenerateControlProperty(nameof(Target), "isOn", "Value", Setter = "SetIsOnWithoutNotify")]
    [GenerateControlEvent(nameof(Target), "onValueChanged", NotifyProperty = "Value")]
    public sealed partial class ToggleElement : Element, IUIAutomationInput<bool>
    {
        private Toggle target;

        private Toggle Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Toggle>();
                }

                return target;
            }
        }

        UnityEngine.UI.Selectable IUIAutomationInput<bool>.InputControl => Target;

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Toggle;

        protected override AccessibilityState NativeAccessibilityState => Value ? AccessibilityState.Checked : AccessibilityState.None;

        bool IUIAutomationInput<bool>.TrySetInput(bool value)
        {
            var control = Target;
            control.isOn = value;
            return true;
        }
    }
}
