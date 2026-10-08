using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Dropdown))]
    public sealed class DropdownElement : Element, IDropdownElement, IUIAutomationInput<int>
    {
        private Dropdown target;
        private bool inputWasEnabled = true;

        public event Action SelectionChanged;

        private Dropdown Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<Dropdown>();
                }

                return target;
            }
        }

        UnityEngine.UI.Selectable IUIAutomationInput<int>.InputControl => Target;

        /// <summary>原生零起始索引；空选项使用 Unity 原生值语义。</summary>
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
                var replacement = new List<Dropdown.OptionData>(count);
                for (var i = 0; i < count; ++i)
                {
                    replacement.Add(new Dropdown.OptionData(value[i] ?? string.Empty));
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
                control.SetValueWithoutNotify(count == 0 ? 0 : Mathf.Clamp(previous, 0, count - 1));
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
            target = RequireComponent<Dropdown>();
            UnityAction<int> handler = value =>
            {
                if (CanReceiveInput(target))
                {
                    NotifyChanged(nameof(Value));
                    if (CanReceiveInput(target))
                    {
                        SelectionChanged?.Invoke();
                    }
                }
            };
            OnDispose(() =>
            {
                SelectionChanged = null;
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
