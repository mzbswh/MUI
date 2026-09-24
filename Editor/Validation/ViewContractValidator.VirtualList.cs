using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        private static void ValidateVirtualList(Element element, Transform root, List<string> errors)
        {
            var serialized = new SerializedObject(element);
            var scroll = serialized.FindProperty("scrollRect").objectReferenceValue as ScrollRect;
            var template = serialized.FindProperty("itemTemplate").objectReferenceValue as View;
            var rowHeight = serialized.FindProperty("rowHeight").floatValue;
            var columns = serialized.FindProperty("columns").intValue;
            var capacity = serialized.FindProperty("maxCells").intValue;
            var overscan = serialized.FindProperty("overscan").intValue;
            var path = Path(element.transform, root);
            ValidateScroll(scroll, element.transform, root, errors);
            if (rowHeight <= 0 || float.IsNaN(rowHeight) || float.IsInfinity(rowHeight))
            {
                errors.Add($"Virtual list row height must be positive and finite: {path}.");
            }

            if (capacity < 1 || columns < 1 || columns > capacity || overscan < 0)
            {
                errors.Add($"Virtual list requires positive capacity/columns, columns <= capacity and nonnegative overscan: {path}.");
            }

            if (serialized.FindProperty("measurementsPerFrame").intValue < 1)
            {
                errors.Add($"Virtual list measurement budget must be positive: {path}.");
            }

            if (template == null)
            {
                errors.Add($"Virtual list item template is missing: {path}.");
            }
            else
            {
                if (template.gameObject.activeSelf)
                {
                    errors.Add($"Virtual list item template must be inactive: {path}.");
                }

                if (!template.transform.IsChildOf(element.transform))
                {
                    errors.Add($"Virtual list item template must belong to the list boundary: {path}.");
                }

                if (!(template.transform is RectTransform))
                {
                    errors.Add($"Virtual list item template requires a RectTransform: {path}.");
                }
            }

            if (serialized.FindProperty("pagePrefetchItems").intValue < 0)
            {
                errors.Add($"Virtual list page prefetch threshold must be nonnegative: {path}.");
            }

            var templates = serialized.FindProperty("itemTemplates");
            var templateKeys = new HashSet<string>(System.StringComparer.Ordinal);
            for (var index = 0; index < templates.arraySize; ++index)
            {
                var entry = templates.GetArrayElementAtIndex(index);
                var key = entry.FindPropertyRelative("key").stringValue;
                var namedView = entry.FindPropertyRelative("view").objectReferenceValue as View;
                if (string.IsNullOrWhiteSpace(key) || !templateKeys.Add(key))
                {
                    errors.Add($"Virtual list named template keys must be non-empty and unique: {path}.");
                }

                if (namedView == null || !(namedView.transform is RectTransform))
                {
                    errors.Add($"Virtual list named template requires a RectTransform View: {path}/{key}.");
                    continue;
                }

                if (namedView.gameObject.activeSelf || !namedView.transform.IsChildOf(element.transform) ||
                    (scroll != null && scroll.content != null && namedView.transform.IsChildOf(scroll.content)))
                {
                    errors.Add($"Virtual list named template must be inactive, inside its boundary and outside content: {path}/{key}.");
                }
            }

            if (scroll == null)
            {
                return;
            }

            if (!scroll.transform.IsChildOf(element.transform))
            {
                errors.Add($"Virtual list ScrollRect must belong to the list boundary: {path}.");
            }

            if (scroll.viewport == null)
            {
                errors.Add($"Virtual list requires an explicit viewport reference: {path}.");
            }

            if (scroll.GetComponent<ScrollRectElement>() != null)
            {
                errors.Add($"Virtual list and ScrollRectElement cannot own the same ScrollRect: {Path(scroll.transform, root)}.");
            }

            var content = scroll.content;
            if (content == null)
            {
                return;
            }

            if (content.childCount != 0)
            {
                errors.Add($"Virtual list content must be empty before initialization: {path}.");
            }

            if (content.GetComponent<LayoutGroup>() != null || content.GetComponent<ContentSizeFitter>() != null)
            {
                errors.Add($"Virtual list owns content layout; remove LayoutGroup/ContentSizeFitter from content: {path}.");
            }

            if (template != null && template.transform.IsChildOf(content))
            {
                errors.Add($"Virtual list item template must be outside content: {path}.");
            }
        }
    }
}
