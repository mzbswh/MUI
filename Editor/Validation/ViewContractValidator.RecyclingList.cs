using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

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
                errors.Add($"回收列表容量必须大于零：{path}。");
            }

            if (!IsInsideListBoundary(element.transform, content, false))
            {
                errors.Add($"回收列表需要边界内独立的内容子节点，引用不能穿过其他 View 或 Element 边界：{path}。");
            }
            else if (content.childCount != 0)
            {
                errors.Add($"回收列表内容节点必须为空，条目由框架创建：{path}。");
            }

            if (template == null)
            {
                errors.Add($"回收列表缺少 NestedViewElement 模板：{path}。");
                return;
            }

            if (!(template.transform is RectTransform) || template.gameObject.activeSelf ||
                !IsInsideListBoundary(element.transform, template.transform, true))
            {
                errors.Add($"回收列表模板必须是边界内非激活的 RectTransform 子节点，引用不能穿过其他 View 或 Element 边界：{path}。");
            }

            if (content != null && (template.transform.IsChildOf(content) || content.IsChildOf(template.transform)))
            {
                errors.Add($"回收列表的模板与内容节点不能互相包含：{path}。");
            }

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
                        errors.Add($"回收列表模板有多个直属 View，需显式指定 childView：{path}。");
                        return;
                    }

                    child = candidate;
                }
            }

            if (template.GetComponent<View>() != null || child == null ||
                child.transform == template.transform || !child.transform.IsChildOf(template.transform))
            {
                errors.Add($"回收列表模板必须用 NestedViewElement 包装一个有效子 View：{path}。");
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
