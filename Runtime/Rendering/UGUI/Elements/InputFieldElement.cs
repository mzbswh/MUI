using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(InputField))]
    [GenerateControlProperty(nameof(Target), "interactable", "Interactable")]
    [GenerateControlProperty(nameof(Target), "text", "Value", Setter = "SetTextWithoutNotify")]
    [GenerateControlEvent(nameof(Target), "onValueChanged", NotifyProperty = "Value", Condition = nameof(PublishChangesWhileEditing))]
    [GenerateControlEvent(nameof(Target), "onEndEdit", "EditingEnded", NotifyProperty = "Value")]
    public sealed partial class InputFieldElement : Element, IUIAutomationInput<string>
    {
        [SerializeField] private TextInputCommitMode commitMode;
        private InputField target;

        /// <summary>反向值通知时机；模型赋值始终更新文本，切换模式不提交当前草稿。</summary>
        public TextInputCommitMode CommitMode
        {
            get
            {
                RequireAlive();
                return commitMode;
            }
            set
            {
                RequireAlive();
                if (!Enum.IsDefined(typeof(TextInputCommitMode), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                if (commitMode != value)
                {
                    commitMode = value;
                    NotifyChanged();
                }
            }
        }

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

        private bool PublishChangesWhileEditing => commitMode == TextInputCommitMode.OnChange;

        protected override void OnInitialize()
        {
            if (!Enum.IsDefined(typeof(TextInputCommitMode), commitMode))
                throw new InvalidOperationException("Invalid text input commit mode.");
            target = RequireComponent<InputField>();
        }
    }
}
