using System;
using System.Collections.Generic;
using MUI.UGUI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MUI.TMP
{
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_Dropdown))]
    public sealed class TMPDropdownElement : Element, IUIAutomationInput<int>
    {
        private TMP_Dropdown target;
        private bool inputWasEnabled = true;

        private TMP_Dropdown Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<TMP_Dropdown>();
                }

                return target;
            }
        }

        UnityEngine.UI.Selectable IUIAutomationInput<int>.InputControl => Target;

        /// <summary>原生索引；配置占位项时，-1 表示未选择。</summary>
        public int Value
        {
            get => Target.value;
            set
            {
                var control = Target;
                var previous = control.value;
                control.SetValueWithoutNotify(value);
                if (previous != control.value)
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>仅支持文本选项，赋值时复制；会替换 Inspector 中配置的选项图片。</summary>
        public IReadOnlyList<string> Options
        {
            get
            {
                var options = Target.options;
                var snapshot = new string[options.Count];
                for (var i = 0; i < snapshot.Length; ++i)
                {
                    snapshot[i] = options[i].text;
                }

                return Array.AsReadOnly(snapshot);
            }

            set
            {
                var control = Target;
                var count = value == null ? 0 : value.Count;
                var replacement = new List<TMP_Dropdown.OptionData>(count);
                for (var i = 0; i < count; ++i)
                {
                    replacement.Add(new TMP_Dropdown.OptionData(value[i] ?? string.Empty));
                }

                var unchanged = control.options.Count == count;
                for (var i = 0; unchanged && i < count; ++i)
                {
                    unchanged = control.options[i].text == replacement[i].text && control.options[i].image == null;
                }

                if (unchanged)
                {
                    return;
                }

                var previous = control.value;
                // 已展开的原生列表仍持有上一组选项对象。
                control.Hide();
                control.options = replacement;
                if (count == 0)
                {
                    control.ClearOptions();
                }
                else
                {
                    control.SetValueWithoutNotify(Mathf.Clamp(previous, control.placeholder == null ? 0 : -1, count - 1));
                }

                control.RefreshShownValue();
                NotifyChanged(nameof(Options));
                if (IsAlive && control != null && previous != control.value)
                {
                    NotifyChanged(nameof(Value));
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
                if (!value)
                {
                    control.Hide();
                }

                NotifyChanged();
            }
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.Dropdown;

        protected override AccessibilityState NativeAccessibilityState => Target.IsExpanded ? AccessibilityState.Expanded : AccessibilityState.None;

        bool IUIAutomationInput<int>.TrySetInput(int value)
        {
            var control = Target;
            // 自动化只选择真实选项，不将占位项视为可选数据。
            if (value < 0 || value >= control.options.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            control.value = value;
            return true;
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<TMP_Dropdown>();
            UnityAction<int> handler = value =>
            {
                if (CanReceiveInput(target))
                {
                    NotifyChanged(nameof(Value));
                }
            };
            OnDispose(() =>
            {
                if (target == null)
                {
                    return;
                }

                target.onValueChanged.RemoveListener(handler);
                target.Hide();
            });
            target.onValueChanged.AddListener(handler);
        }

        private void LateUpdate()
        {
            // 原生下拉框拥有弹出列表，有效输入门控变化时应关闭。
            if (target == null)
            {
                return;
            }

            var enabled = CanReceiveInput(target);
            if (inputWasEnabled && !enabled)
            {
                target.Hide();
            }

            inputWasEnabled = enabled;
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.Hide();
            }
        }
    }
}
