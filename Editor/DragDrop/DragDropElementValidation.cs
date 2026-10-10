using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor.Extensions
{
    [InitializeOnLoad]
    internal static class DragDropElementValidation
    {
        static DragDropElementValidation()
        {
            ViewContractValidator.RegisterElementValidator<DragSourceElement>((element, view, errors) => ValidateDragDrop(element, view.transform, errors));
            ViewContractValidator.RegisterElementValidator<DropTargetElement>((element, view, errors) => ValidateDragDrop(element, view.transform, errors));
        }

        private static void ValidateDragDrop(Element element, Transform root, ICollection<string> errors)
        {
            if (!(element is DragSourceElement) && !(element is DropTargetElement))
            {
                return;
            }

            var path = AnimationUtility.CalculateTransformPath(element.transform, root);
            var data = new SerializedObject(element);
            if (element is DropTargetElement)
            {
                if (data.FindProperty("maxPendingDrops").intValue < 1)
                {
                    errors.Add(L.Format("editor.DragDropElementValidation.ee19296263", path));
                }

                var highlight = OverlayReference<GameObject>(data, "allowedHighlight");
                if (highlight != null)
                {
                    if (highlight.transform == element.transform || !highlight.transform.IsChildOf(element.transform))
                    {
                        errors.Add(L.Format("editor.DragDropElementValidation.097ff70a04", path));
                    }

                    if (highlight.GetComponentsInChildren<Selectable>(true).Length != 0)
                    {
                        errors.Add(L.Format("editor.DragDropElementValidation.856ef4d269", path));
                    }

                    foreach (var graphic in highlight.GetComponentsInChildren<Graphic>(true))
                    {
                        if (graphic.raycastTarget && graphic.enabled && !RaycastBlocked(graphic.transform, highlight.transform))
                        {
                            errors.Add(L.Format("editor.DragDropElementValidation.edec36e0d3", AnimationUtility.CalculateTransformPath(graphic.transform, root)));
                        }
                    }
                }

                return;
            }

            var icon = OverlayReference<Image>(data, "dragIcon");
            var host = OverlayReference<RectTransform>(data, "ghostRoot");
            if (icon == null && host == null)
            {
                return;
            }

            if (icon == null || host == null)
            {
                errors.Add(L.Format("editor.DragDropElementValidation.165a5f71ed", path));
                return;
            }

            if (!icon.transform.IsChildOf(element.transform))
            {
                errors.Add(L.Format("editor.DragDropElementValidation.332ea75fe2", path));
            }

            if (host.IsChildOf(element.transform))
            {
                errors.Add(L.Format("editor.DragDropElementValidation.dfa4574438", path));
            }

            var sourceCanvas = element.GetComponentInParent<Canvas>(true);
            var targetCanvas = host.GetComponentInParent<Canvas>(true);
            if (sourceCanvas != null && targetCanvas != null && sourceCanvas.rootCanvas != targetCanvas.rootCanvas)
            {
                errors.Add(L.Format("editor.DragDropElementValidation.07c3e3a8ea", path));
            }

            if (targetCanvas != null && targetCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && targetCanvas.rootCanvas.worldCamera == null)
            {
                errors.Add(L.Format("editor.DragDropElementValidation.a8a32fbe33", path));
            }
        }

        private static T OverlayReference<T>(SerializedObject data, string property)
                    where T : UnityEngine.Object => data.FindProperty(property).objectReferenceValue as T;

        private static bool RaycastBlocked(Transform node, Transform content)
        {
            for (var current = node; current != null; current = current.parent)
            {
                var stop = false;
                foreach (var group in current.GetComponents<CanvasGroup>())
                {
                    if (!group.enabled)
                    {
                        continue;
                    }

                    if (!group.blocksRaycasts)
                    {
                        return true;
                    }

                    stop |= group.ignoreParentGroups;
                }

                if (stop || current == content)
                {
                    return false;
                }
            }

            return false;
        }
    }
}
