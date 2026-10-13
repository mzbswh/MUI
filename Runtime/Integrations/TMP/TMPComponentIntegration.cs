using System;
using MUI.Unity;
using MUI.UGUI;
using TMPro;
using UnityEngine;

namespace MUI.TMP
{
    internal static class TMPComponentIntegration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            NativeAutomationInput<string>.Register<TMP_InputField>((control, value) =>
            {
                if (control.readOnly) return false;
                control.text = value ?? string.Empty;
                return true;
            });
            NativeAutomationInput<int>.Register<TMP_Dropdown>((control, value) =>
            {
                if (value < 0 || value >= control.options.Count) throw new ArgumentOutOfRangeException(nameof(value));
                control.value = value;
                return true;
            });
            AccessibilityTree.Register<TMP_Text>(AccessibilityRole.Text, text => text.text);
            AccessibilityTree.Register<TMP_InputField>(AccessibilityRole.TextInput, input => string.Empty);
            AccessibilityTree.Register<TMP_Dropdown>(AccessibilityRole.Dropdown, dropdown => dropdown.captionText == null ? string.Empty : dropdown.captionText.text);
        }
    }
}
