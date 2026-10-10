using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        private static void ValidateRecyclingList(Element element, Transform root, List<string> errors)
        {
            var serialized = new SerializedObject(element);
            var content = serialized.FindProperty("content").objectReferenceValue as RectTransform;
            var template = serialized.FindProperty("itemTemplate").objectReferenceValue as NestedViewElement;
            var path = Path(element.transform, root);
            if (serialized.FindProperty("capacity").intValue < 1)
            {
                errors.Add(L.Format("editor.ViewContractValidator.RecyclingList.23a28f3e0f", path));
            }

            if (!IsInsideListBoundary(element.transform, content, false))
            {
                errors.Add(L.Format("editor.ViewContractValidator.RecyclingList.80fa4d3c4b", path));
            }
            else if (content.childCount != 0)
            {
                errors.Add(L.Format("editor.ViewContractValidator.RecyclingList.e5160942f1", path));
            }

            if (template == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.RecyclingList.a48fe7d434", path));
                return;
            }

            if (!(template.transform is RectTransform) || template.gameObject.activeSelf ||
                !IsInsideListBoundary(element.transform, template.transform, true))
            {
                errors.Add(L.Format("editor.ViewContractValidator.RecyclingList.1b9153195e", path));
            }

            if (content != null && (template.transform.IsChildOf(content) || content.IsChildOf(template.transform)))
            {
                errors.Add(L.Format("editor.ViewContractValidator.RecyclingList.7ece7c5b40", path));
            }

            ValidateNestedListChild(template, path, errors);
        }

        private static void ValidateNestedListChild(NestedViewElement template, string path, List<string> errors)
        {
            var nested = new SerializedObject(template);
            var child = nested.FindProperty("childView").objectReferenceValue as View;
            if (child == null)
            {
                foreach (Transform node in template.transform)
                {
                    var candidate = node.GetComponent<View>();
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (child != null)
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.RecyclingList.72c81031ca", path));
                        return;
                    }

                    child = candidate;
                }
            }

            if (template.GetComponent<View>() != null || child == null ||
                child.transform == template.transform || !child.transform.IsChildOf(template.transform))
            {
                errors.Add(L.Format("editor.ViewContractValidator.RecyclingList.c30d9540d5", path));
            }
        }

        /// <summary>与运行时按相同路径检查，包含非激活节点，但不初始化任何控件。</summary>
        private static bool IsInsideListBoundary(Transform owner, Transform target, bool allowTargetBoundary)
        {
            if (target == null || target == owner)
            {
                return false;
            }

            for (var node = target; node != owner; node = node.parent)
            {
                if (node == null)
                {
                    return false;
                }

                if (node == target && allowTargetBoundary)
                {
                    continue;
                }

                if (node.GetComponent<View>() != null)
                {
                    return false;
                }

                foreach (var component in node.GetComponents<MonoBehaviour>())
                {
                    if (component != null && component is IElementBoundary)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
