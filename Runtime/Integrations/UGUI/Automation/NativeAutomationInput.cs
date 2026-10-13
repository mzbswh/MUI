using MUI.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    internal static class UGUIAutomationInputs
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            NativeAutomationInput<bool>.Register<Toggle>((control, value) => { control.isOn = value; return true; });
            NativeAutomationInput<float>.Register<Slider>((control, value) => { RequireFinite(value); control.value = value; return true; });
            NativeAutomationInput<float>.Register<Scrollbar>((control, value) => { RequireFinite(value); control.value = value; return true; });
            NativeAutomationInput<string>.Register<InputField>((control, value) => { if (control.readOnly) return false; control.text = value ?? string.Empty; return true; });
            NativeAutomationInput<int>.Register<Dropdown>((control, value) =>
            {
                if (value < 0 || value >= control.options.Count) throw new ArgumentOutOfRangeException(nameof(value));
                control.value = value; return true;
            });
        }
        private static void RequireFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
