using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    internal static class ViewValidationMenu
    {
        private const string MenuPath = "Tools/MUI/校验所选视图结构 (Validate Selected View Structures)";

        [MenuItem(MenuPath, true)]
        private static bool CanValidate() => !EditorApplication.isPlayingOrWillChangePlaymode && Selection.gameObjects.Length != 0;

        [MenuItem(MenuPath)]
        private static void ValidateSelected()
        {
            if (!CanValidate())
            {
                return;
            }

            var views = new List<View>();
            var seen = new HashSet<View>();
            foreach (var root in Selection.gameObjects)
            {
                if (root == null)
                {
                    continue;
                }

                // 直接读取所选资源或场景层级，无需实例化 Prefab、
                // 初始化原生控件、切换场景或保存。
                foreach (var view in root.GetComponentsInChildren<View>(true))
                {
                    if (view != null && seen.Add(view))
                    {
                        views.Add(view);
                    }
                }
            }

            var inspected = 0;
            var failed = 0;
            var issues = 0;
            var cancelled = false;
            try
            {
                foreach (var view in views)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(L.Get("editor.ViewValidationMenu.b15d9cec82"), view.name, inspected / (float)views.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    ++inspected;
                    try
                    {
                        var results = ViewContractValidator.ValidateStructure(view);
                        if (results.Count != 0)
                        {
                            ++failed;
                        }

                        issues += results.Count;
                        foreach (var result in results)
                        {
                            Debug.LogError(L.Format("editor.ViewValidationMenu.b66582a936", view.name, result), view);
                        }
                    }
                    catch (Exception error)
                    {
                        ++failed;
                        ++issues;
                        Debug.LogException(error, view);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(L.Format("editor.ViewValidationMenu.890cc9480b", L.Get(cancelled ? "value.cancelled" : "value.finished"), inspected, views.Count, failed, issues));
        }
    }
}
