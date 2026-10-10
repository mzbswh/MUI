using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        private static void ValidateVirtualList(Element element, Transform root, List<string> errors)
        {
            var serialized = new SerializedObject(element);
            var scroll = serialized.FindProperty("scrollRect").objectReferenceValue as ScrollRect;
            var template = serialized.FindProperty("itemTemplate").objectReferenceValue as View;
            var estimatedItemExtent = serialized.FindProperty("estimatedItemExtent").floatValue;
            var columns = serialized.FindProperty("columns").intValue;
            var capacity = serialized.FindProperty("maxCells").intValue;
            var overscan = serialized.FindProperty("overscan").intValue;
            var path = Path(element.transform, root);
            var spacing = serialized.FindProperty("spacing").vector2Value;
            var padding = serialized.FindProperty("padding");
            if (spacing.x < 0 || spacing.y < 0 || float.IsNaN(spacing.x) || float.IsNaN(spacing.y) ||
                float.IsInfinity(spacing.x) || float.IsInfinity(spacing.y))
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.cc31c4f87c", path));
            }
            foreach (var side in new[] { "m_Left", "m_Right", "m_Top", "m_Bottom" })
            {
                var inset = padding.FindPropertyRelative(side);
                if (inset == null || inset.intValue < 0)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualList.cd7fadd5fc", path));
                    break;
                }
            }
            var followTolerance = serialized.FindProperty("endFollowTolerance").floatValue;
            if (followTolerance < 0 || float.IsNaN(followTolerance) || float.IsInfinity(followTolerance))
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.413554c7ee", path));
            }
            var axis = (RectTransform.Axis)serialized.FindProperty("scrollAxis").intValue;
            if ((axis != RectTransform.Axis.Horizontal && axis != RectTransform.Axis.Vertical) ||
                (axis == RectTransform.Axis.Horizontal && columns != 1))
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.1ec8ef741e", path));
            }
            if (serialized.FindProperty("automaticColumns").boolValue)
            {
                var minimumWidth = serialized.FindProperty("minimumColumnWidth").floatValue;
                if (axis != RectTransform.Axis.Vertical || minimumWidth <= 0 ||
                    float.IsNaN(minimumWidth) || float.IsInfinity(minimumWidth))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualList.b08e766b89", path));
                }
            }
            ValidateScroll(scroll, element.transform, root, errors);
            if (estimatedItemExtent <= 0 || float.IsNaN(estimatedItemExtent) || float.IsInfinity(estimatedItemExtent))
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.bf3ae6ec6b", path));
            }

            if (capacity < 1 || columns < 1 || columns > capacity || overscan < 0)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.91c54ff8f0", path));
            }

            if (serialized.FindProperty("maxRevealCorrections").intValue < 1)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.cfc374c27a", path));
            }

            if (serialized.FindProperty("measurementsPerFrame").intValue < 1)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.cbd8e6c32d", path));
            }

            if (template == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.922d53d064", path));
            }
            else
            {
                if (template.gameObject.activeSelf)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualList.5cbde16ef0", path));
                }

                if (!template.transform.IsChildOf(element.transform))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualList.e5e8afe21e", path));
                }

                if (!(template.transform is RectTransform))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualList.bc3ba759d5", path));
                }
            }

            var templates = serialized.FindProperty("itemTemplates");
            ValidateVirtualFailureTemplate(element, serialized, scroll, template, root, errors);
            var templateKeys = new HashSet<string>(System.StringComparer.Ordinal);
            for (var index = 0; index < templates.arraySize; ++index)
            {
                var entry = templates.GetArrayElementAtIndex(index);
                var key = entry.FindPropertyRelative("key").stringValue;
                var namedView = entry.FindPropertyRelative("view").objectReferenceValue as View;
                if (string.IsNullOrWhiteSpace(key) || !templateKeys.Add(key))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualList.544ef3258d", path));
                }

                if (namedView == null || !(namedView.transform is RectTransform))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualList.d51750887f", path, key));
                    continue;
                }

                if (namedView.gameObject.activeSelf || !namedView.transform.IsChildOf(element.transform) ||
                    (scroll != null && scroll.content != null && namedView.transform.IsChildOf(scroll.content)))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.VirtualList.aa0d9f0ab5", path, key));
                }
            }

            if (scroll == null)
            {
                return;
            }

            if (!scroll.transform.IsChildOf(element.transform))
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.89039a932d", path));
            }

            if (scroll.viewport == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.68fc07fd71", path));
            }

            if (scroll.GetComponent<ScrollRectElement>() != null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.f8192fe74e", Path(scroll.transform, root)));
            }

            var content = scroll.content;
            if (content == null)
            {
                return;
            }

            if (content.childCount != 0)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.0028a0908f", path));
            }

            if (content.GetComponent<LayoutGroup>() != null || content.GetComponent<ContentSizeFitter>() != null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.b4082bbf93", path));
            }

            if (template != null && template.transform.IsChildOf(content))
            {
                errors.Add(L.Format("editor.ViewContractValidator.VirtualList.cbeb11bf2d", path));
            }
        }
    }
}
