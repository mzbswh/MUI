using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Toggle))]
    public sealed class ToggleElement : Element, IUIAutomationInput<bool>
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

        public bool Value
        {
            get => Target.isOn;
            set
            {
                if (Target.isOn == value)
                {
                    return;
                }

                Target.SetIsOnWithoutNotify(value);
                NotifyChanged();
            }
        }

        public bool Interactable
        {
            get => Target.interactable;
            set
            {
                if (Target.interactable == value)
                {
                    return;
                }

                Target.interactable = value;
                NotifyChanged();
            }
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Toggle;

        protected override AccessibilityState NativeAccessibilityState => Value ? AccessibilityState.Checked : AccessibilityState.None;

        bool IUIAutomationInput<bool>.TrySetInput(bool value)
        {
            var control = Target;
            control.isOn = value;
            return true;
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<Toggle>();
            UnityAction<bool> handler = value =>
            {
                if (CanReceiveInput(target))
                {
                    NotifyChanged(nameof(Value));
                }
            };
            OnDispose(() =>
            {
                if (target != null)
                {
                    target.onValueChanged.RemoveListener(handler);
                }
            });
            target.onValueChanged.AddListener(handler);
        }
    }
}
