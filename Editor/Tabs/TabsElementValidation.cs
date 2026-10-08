using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Editor.Extensions
{
    /// <summary>只读检查 Tab 创作结构，不初始化控件、生成按钮或加载子界面。</summary>
    [InitializeOnLoad]
    internal static class TabsElementValidation
    {
        static TabsElementValidation()
        {
            ViewContractValidator.RegisterElementValidator<TabBarElement>(ValidateBar);
            ViewContractValidator.RegisterElementValidator<AsyncContentElement>(ValidateContent);
        }

        private static void ValidateBar(TabBarElement element, View view, ICollection<string> errors)
        {
            var data = new SerializedObject(element);
            var path = AnimationUtility.CalculateTransformPath(element.transform, view.transform);
            var template = Reference<Button>(data, "buttonTemplate");
            if (template != null)
            {
                if (template.gameObject.activeSelf)
                {
                    errors.Add($"Tab 按钮模板必须保持未激活：{path}。");
                }

                if (template.onClick.GetPersistentEventCount() != 0)
                {
                    errors.Add($"Tab 按钮模板不能配置持久化点击事件：{path}。");
                }

                if (template.GetComponentsInChildren<View>(true).Length != 0 || template.GetComponentsInChildren<Element>(true).Length != 0)
                {
                    errors.Add($"Tab 按钮模板不能包含 MUI View 或 Element：{path}。");
                }
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            var buttons = new HashSet<Button>();
            var entries = data.FindProperty("entries");
            if (entries.arraySize == 0)
            {
                // 与运行时一致：未配置条目时只发现直接子节点上的 Button，排除模板。
                foreach (Transform child in element.transform)
                {
                    var button = child.GetComponent<Button>();
                    if (button == null || button == template)
                    {
                        continue;
                    }

                    var mark = child.Find("Selected");
                    ValidateEntry(element.transform, child.name, button, mark, template, keys, buttons, path, errors);
                }
            }
            else
            {
                for (var index = 0; index < entries.arraySize; ++index)
                {
                    var entry = entries.GetArrayElementAtIndex(index);
                    var button = entry.FindPropertyRelative("Button").objectReferenceValue as Button;
                    var mark = entry.FindPropertyRelative("SelectionMark").objectReferenceValue as GameObject;
                    ValidateEntry(element.transform, entry.FindPropertyRelative("Key").stringValue, button, mark == null ? null : mark.transform, template, keys, buttons, path, errors);
                }
            }
        }

        private static void ValidateEntry(Transform root,
            string key,
            Button button,
            Transform mark,
            Button template,
            HashSet<string> keys,
            HashSet<Button> buttons,
            string path,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(key) || !keys.Add(key))
            {
                errors.Add($"Tab 键必须非空且唯一：{path}，键 '{key}'。");
            }

            if (button == null)
            {
                errors.Add($"Tab 条目缺少 Button：{path}，键 '{key}'。");
                return;
            }

            if (!buttons.Add(button))
            {
                errors.Add($"同一个 Button 不能绑定多个 Tab 条目：{path}，键 '{key}'。");
            }

            if (button == template || button.transform == root || !button.transform.IsChildOf(root))
            {
                errors.Add($"Tab 按钮必须位于 TabBar 子层级且不能兼作模板：{path}，键 '{key}'。");
            }

            if (mark != null && (mark == button.transform || !mark.IsChildOf(button.transform)))
            {
                errors.Add($"Tab 选择标记必须是对应按钮的严格子节点：{path}，键 '{key}'。");
            }
        }

        private static void ValidateContent(AsyncContentElement element, View view, ICollection<string> errors)
        {
            var data = new SerializedObject(element);
            var root = element.transform;
            var path = AnimationUtility.CalculateTransformPath(root, view.transform);
            var content = Reference<Transform>(data, "contentRoot");
            if (content == null)
            {
                content = root.Find("ContentHost");
            }

            if (content == null || content == root || !content.IsChildOf(root))
            {
                errors.Add($"异步内容区必须具有子级 ContentHost：{path}。");
                return;
            }

            var parent = root.parent;
            if (parent != null)
            {
                foreach (Transform sibling in parent)
                {
                    if (sibling.GetComponent<TabBarElement>() != null && sibling.GetSiblingIndex() <= root.GetSiblingIndex())
                    {
                        errors.Add($"TabBar 必须排在同级内容区之后，才能显示在内容上方：{path}。");
                    }
                }
            }

            foreach (var canvas in element.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.overrideSorting)
                {
                    errors.Add($"内容区 Canvas 不能启用 Override Sorting：{AnimationUtility.CalculateTransformPath(canvas.transform, view.transform)}。");
                }
            }

            // 缺省引用的自动发现规则必须与控件初始化保持一致。
            var loading = Reference<GameObject>(data, "loadingOverlay");
            ValidateOverlay(root, content, loading == null ? root.Find("LoadingOverlay") : loading.transform, "加载提示", path, errors);
            var error = Reference<Text>(data, "errorLabel");
            var errorNode = root.Find("ErrorOverlay");
            if (error == null && errorNode != null)
            {
                error = errorNode.GetComponent<Text>();
            }

            ValidateOverlay(root, content, error == null ? null : error.transform, "错误提示", path, errors);
            var retry = Reference<Button>(data, "retryButton");
            var retryNode = root.Find("Retry");
            if (retry == null && retryNode != null)
            {
                retry = retryNode.GetComponent<Button>();
            }

            ValidateOverlay(root, content, retry == null ? null : retry.transform, "重试按钮", path, errors);
        }

        private static void ValidateOverlay(Transform root,
            Transform content,
            Transform overlay,
            string label,
            string path,
            ICollection<string> errors)
        {
            if (overlay != null && (overlay == root || !overlay.IsChildOf(root) || overlay.IsChildOf(content) || content.IsChildOf(overlay)))
            {
                errors.Add($"{label}必须位于内容区内、ContentHost 外，且不能包含 ContentHost：{path}。");
            }
        }

        private static T Reference<T>(SerializedObject data, string name)
            where T : UnityEngine.Object
        {
            return data.FindProperty(name).objectReferenceValue as T;
        }
    }
}
