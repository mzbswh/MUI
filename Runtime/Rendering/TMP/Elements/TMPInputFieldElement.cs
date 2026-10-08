using System;
using MUI.UGUI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MUI.TMP
{
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_InputField))]
    public sealed partial class TMPInputFieldElement : Element, IInputFieldElement, IUIAutomationInput<string>
    {
        [SerializeField] private TextInputCommitMode commitMode;
        private TMP_InputField target;

        /// <summary>原生编辑结束通知，包含失去焦点的情况。</summary>
        public event Action EditingEnded;

        /// <summary>原生 TMP 提交通知，不保证输入法组合输入已经结束。</summary>
        public event Action Submitted;

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
                var control = Target;
                if (control.interactable == value)
                {
                    return;
                }

                control.interactable = value;
                if (IsAlive)
                {
                    NotifyChanged();
                }
            }
        }

        public bool ReadOnly
        {
            get => Target.readOnly;
            set
            {
                var control = Target;
                if (control.readOnly == value)
                {
                    return;
                }

                control.readOnly = value;
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

            // TMP 原生文本赋值不执行逐字输入校验或字符数限制。
            control.text = value ?? string.Empty;
            return true;
        }

        protected override void OnInitialize()
        {
            if (!Enum.IsDefined(typeof(TextInputCommitMode), commitMode))
            {
                throw new InvalidOperationException("Invalid text input commit mode.");
            }

            target = RequireComponent<TMP_InputField>();
            UnityAction<string> changed = value =>
            {
                if (commitMode == TextInputCommitMode.OnChange && CanReceiveInput(target))
                {
                    NotifyChanged(nameof(Value));
                }
            };
            UnityAction<string> ended = value => PublishEditingEvent(false);
            UnityAction<string> submitted = value => PublishEditingEvent(true);
            OnDispose(() =>
            {
                if (target != null)
                {
                    target.onValueChanged.RemoveListener(changed);
                    target.onEndEdit.RemoveListener(ended);
                    target.onSubmit.RemoveListener(submitted);
                }

                EditingEnded = null;
                Submitted = null;
            });
            target.onValueChanged.AddListener(changed);
            target.onEndEdit.AddListener(ended);
            target.onSubmit.AddListener(submitted);
        }

        private void PublishEditingEvent(bool submitted)
        {
            if (!CanReceiveInput(target))
            {
                return;
            }

            // 命令执行前反向绑定先读取规范化文本；该过程可能关闭 View。
            NotifyChanged(nameof(Value));
            if (!CanReceiveInput(target))
            {
                return;
            }

            if (submitted)
            {
                Submitted?.Invoke();
            }
            else
            {
                EditingEnded?.Invoke();
            }
        }
    }
}
