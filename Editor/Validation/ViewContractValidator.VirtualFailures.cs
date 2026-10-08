using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        private static void ValidateVirtualFailureTemplate(Element element, SerializedObject serialized,
            UnityEngine.UI.ScrollRect scroll, View template, Transform root, List<string> errors)
        {
            var visual = serialized.FindProperty("itemFailureTemplate").objectReferenceValue as GameObject;
            if (visual == null)
            {
                return;
            }

            var node = visual.transform;
            var path = Path(element.transform, root);
            if (!(node is RectTransform) || visual.activeSelf || !node.IsChildOf(element.transform) ||
                (scroll != null && scroll.content != null &&
                    (node.IsChildOf(scroll.content) || scroll.content.IsChildOf(node))) ||
                (template != null && (node.IsChildOf(template.transform) || template.transform.IsChildOf(node))))
            {
                errors.Add($"虚拟列表失败模板须为非激活 RectTransform，位于边界内、Content 和条目模板外：{path}。");
            }

            var templates = serialized.FindProperty("itemTemplates");
            for (var index = 0; index < templates.arraySize; ++index)
            {
                var view = templates.GetArrayElementAtIndex(index).FindPropertyRelative("view").objectReferenceValue as View;
                if (view != null && (node.IsChildOf(view.transform) || view.transform.IsChildOf(node)))
                {
                    errors.Add($"失败模板不能与命名条目模板互相包含：{path}。");
                }
            }

            if (visual.GetComponentInChildren<View>(true) != null || visual.GetComponentInChildren<Element>(true) != null)
            {
                errors.Add($"失败模板只负责视觉表现，不能包含 View 或 Element 所有权：{path}。");
            }

            foreach (var group in visual.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (group.ignoreParentGroups)
                {
                    errors.Add($"失败模板不能跳过父级 CanvasGroup 输入门控：{path}。");
                }
            }
        }
    }
}
