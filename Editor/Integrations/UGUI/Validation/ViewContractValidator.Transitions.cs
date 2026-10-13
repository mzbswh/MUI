using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        private static void ValidateTransitions(View view, List<string> errors)
        {
            // 读取序列化创作数据，不初始化 View 或查询运行时所有权。
            var serialized = new SerializedObject(view);
            var duration = serialized.FindProperty("enterFadeDuration").floatValue;
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0)
            {
                errors.Add(L.Get("editor.ViewContractValidator.Transitions.453712be0b"));
            }

            var exitDuration = serialized.FindProperty("exitFadeDuration").floatValue;
            if (float.IsNaN(exitDuration) || float.IsInfinity(exitDuration) || exitDuration < 0)
            {
                errors.Add(L.Get("editor.ViewContractValidator.Transitions.a533a7eab7"));
            }

            var group = view.GetComponent<CanvasGroup>();
            if (group == null)
            {
                errors.Add(L.Get("editor.ViewContractValidator.Transitions.f07ffe69c5"));
            }
        }
    }
}
