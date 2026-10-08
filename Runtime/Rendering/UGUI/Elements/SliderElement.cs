using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Slider))]
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

        public float Value
        {
            get => Target.value;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new System.ArgumentOutOfRangeException(nameof(value), "滑动条数值必须为有限值。");
                }

                var old = Target.value;
                Target.SetValueWithoutNotify(value);
                if (!old.Equals(Target.value))
                {
                    NotifyChanged();
                }
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

        protected override void OnInitialize()
        {
            target = RequireComponent<Slider>();
            UnityAction<float> handler = value =>
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
