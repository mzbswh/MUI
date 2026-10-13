using System;
using MUI.UGUI;
using TMPro;
using UnityEngine;

namespace MUI.TMP
{
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_InputField))]
    [GenerateControlProperty(nameof(Target), "interactable", "Interactable")]
    [GenerateControlProperty(nameof(Target), "readOnly", "ReadOnly")]
    [GenerateControlProperty(nameof(Target), "text", "Value", Setter = "SetTextWithoutNotify")]
    [GenerateControlEvent(nameof(Target), "onValueChanged", NotifyProperty = "Value", Condition = nameof(PublishChangesWhileEditing))]
    [GenerateControlEvent(nameof(Target), "onEndEdit", "EditingEnded", NotifyProperty = "Value")]
    [GenerateControlEvent(nameof(Target), "onSubmit", "Submitted", NotifyProperty = "Value")]
    public sealed partial class TMPInputFieldElement : Element, IUIAutomationInput<string>
    {
        [SerializeField] private TextInputCommitMode commitMode;
        private TMP_InputField target;

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

        private TMP_InputField Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<TMP_InputField>();
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

            // TMP 原生文本赋值不执行逐字输入校验或字符数限制。
            control.text = value ?? string.Empty;
            return true;
        }

        private bool PublishChangesWhileEditing => commitMode == TextInputCommitMode.OnChange;

        protected override void OnInitialize()
        {
            if (!Enum.IsDefined(typeof(TextInputCommitMode), commitMode))
                throw new InvalidOperationException("Invalid text input commit mode.");
            target = RequireComponent<TMP_InputField>();
        }
    }
}
