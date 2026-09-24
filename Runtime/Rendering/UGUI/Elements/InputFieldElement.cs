using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(InputField))]
    public sealed partial class InputFieldElement : Element, IUIAutomationInput<string>
    {
        private InputField target;

        /// <summary>原生编辑结束通知，包含失焦，并非只在按回车提交时触发。</summary>
        public event Action EditingEnded;

        private InputField Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<InputField>();
                }

                return target;
            }
        }

        UnityEngine.UI.Selectable IUIAutomationInput<string>.InputControl => Target;

        public string Value
        {
            get => Target.text;
            set
            {
                var control = Target;
                var previous = control.text;
                if (previous == value)
                {
                    return;
                }

                control.SetTextWithoutNotify(value);
                // 原生校验可能规范化文本或执行项目回调，只发布实际存活控件的变化。
                if (IsAlive && control != null && previous != control.text)
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

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.TextInput;

        protected override AccessibilityState NativeAccessibilityState => Target.readOnly ? AccessibilityState.ReadOnly : AccessibilityState.None;

        bool IUIAutomationInput<string>.TrySetInput(string value)
        {
            var control = Target;
            if (control.readOnly)
            {
                return false;
            }

            control.text = value ?? string.Empty;
            return true;
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<InputField>();
            UnityAction<string> handler = value =>
            {
                if (CanReceiveInput(target))
                {
                    NotifyChanged(nameof(Value));
                }
            };
            UnityAction<string> ended = value =>
            {
                if (!CanReceiveInput(target))
                {
                    return;
                }

                // 调用业务命令前，先发布规范化后的当前文本。
                NotifyChanged(nameof(Value));
                if (CanReceiveInput(target))
                {
                    EditingEnded?.Invoke();
                }
            };
            OnDispose(() =>
            {
                if (target != null)
                {
                    target.onValueChanged.RemoveListener(handler);
                    target.onEndEdit.RemoveListener(ended);
                }

                EditingEnded = null;
            });
            target.onValueChanged.AddListener(handler);
            target.onEndEdit.AddListener(ended);
        }
    }
}
