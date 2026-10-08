using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
                    errors.Add($"Drop target capacity must be positive: {path}.");
                }

                var highlight = OverlayReference<GameObject>(data, "allowedHighlight");
                if (highlight != null)
                {
                    if (highlight.transform == element.transform || !highlight.transform.IsChildOf(element.transform))
                    {
                        errors.Add($"Drop highlight must be a strict child of the target: {path}.");
                    }

                    if (highlight.GetComponentsInChildren<Selectable>(true).Length != 0)
                    {
                        errors.Add($"Drop highlight must be decorative, without Selectable children: {path}.");
                    }

                    foreach (var graphic in highlight.GetComponentsInChildren<Graphic>(true))
                    {
                        if (graphic.raycastTarget && graphic.enabled && !RaycastBlocked(graphic.transform, highlight.transform))
                        {
                            errors.Add($"Drop highlight must not intercept pointer raycasts: {AnimationUtility.CalculateTransformPath(graphic.transform, root)}.");
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
                errors.Add($"Drag ghost requires both Drag Icon and Ghost Root: {path}.");
                return;
            }

            if (!icon.transform.IsChildOf(element.transform))
            {
                errors.Add($"Drag Icon must belong to the source hierarchy: {path}.");
            }

            if (host.IsChildOf(element.transform))
            {
                errors.Add($"Ghost Root must be outside the source hierarchy: {path}.");
            }

            var sourceCanvas = element.GetComponentInParent<Canvas>(true);
            var targetCanvas = host.GetComponentInParent<Canvas>(true);
            if (sourceCanvas != null && targetCanvas != null && sourceCanvas.rootCanvas != targetCanvas.rootCanvas)
            {
                errors.Add($"Drag ghost and source use different root Canvases: {path}.");
            }

            if (targetCanvas != null && targetCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && targetCanvas.rootCanvas.worldCamera == null)
            {
                errors.Add($"Camera/world-space ghost requires an explicit root Canvas camera: {path}.");
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
