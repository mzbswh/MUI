using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

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
                errors.Add(L.Format("editor.ViewContractValidator.VirtualFailures.167f053c4d", path));
            }

            var templates = serialized.FindProperty("itemTemplates");
            for (var index = 0; index < templates.arraySize; ++index)
            {
                var view = templates.GetArrayElementAtIndex(index).FindPropertyRelative("view").objectReferenceValue as View;
                if (view != null && (node.IsChildOf(view.transform) || view.transform.IsChildOf(node)))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualFailures.2c461a7488", path));
                }
            }

            if (visual.GetComponentInChildren<View>(true) != null || visual.GetComponentInChildren<Element>(true) != null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualFailures.ef171c0ca4", path));
            }

            foreach (var group in visual.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (group.ignoreParentGroups)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualFailures.1ae15331b1", path));
                }
            }
        }
    }
}
