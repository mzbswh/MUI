using System;
using System.Collections.Generic;
using MUI.UGUI;
using TMPro;
using UnityEngine;

namespace MUI.TMP
{
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_Dropdown))]
    [GenerateControlProperty(nameof(Target), "interactable", "Interactable")]
    [GenerateControlProperty(nameof(Target), "value", "Value", Setter = "SetValueWithoutNotify")]
    [GenerateControlEvent(nameof(Target), "onValueChanged", "SelectionChanged", NotifyProperty = "Value")]
    public sealed partial class TMPDropdownElement : Element, IUIAutomationInput<int>
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
            // 弹出列表拥有独立原生对象，最终释放必须关闭它。
            OnDispose(() => { if (target != null) target.Hide(); });
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
