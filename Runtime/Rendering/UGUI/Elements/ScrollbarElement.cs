using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>独立滚动条适配，原生滚动事件受 View 输入门控约束。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Scrollbar))]
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

        public float Value
        {
            get => Target.value;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new System.ArgumentOutOfRangeException(nameof(value), "滚动条数值必须为有限值。");
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
            target = RequireComponent<Scrollbar>();
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
