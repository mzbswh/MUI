using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        private static void ValidateOverlayElement(Element element, Transform root, List<string> errors)
        {
            if (!(element is AnchoredOverlayElement))
            {
                return;
            }

            var data = new SerializedObject(element);
            var path = Path(element.transform, root);
            if (element is AnchoredOverlayElement)
            {
                var content = OverlayReference<RectTransform>(data, "content");
                if (content == null || content.parent != element.transform)
                {
                    errors.Add($"Overlay content must be a direct RectTransform child: {path}.");
                }

                if (content != null)
                {
                    if (content.localScale != Vector3.one || Quaternion.Angle(content.localRotation, Quaternion.identity) > 0.01f)
                    {
                        errors.Add($"Overlay content requires unit scale and identity rotation: {path}.");
                    }

                    var layout = element.GetComponent<LayoutGroup>();
                    var layoutElement = content.GetComponent<LayoutElement>();
                    if (layout != null && layout.enabled && (layoutElement == null || !layoutElement.ignoreLayout))
                    {
                        errors.Add($"Parent LayoutGroup competes with overlay placement; exclude content from layout: {path}.");
                    }
                }

                var side = data.FindProperty("placement").intValue;
                if (!Enum.IsDefined(typeof(OverlayPlacement), side))
                {
                    errors.Add($"Unknown overlay placement: {path}.");
                }

                ValidateOverlayNumber(data, "gap", path, errors);
                ValidateOverlayNumber(data, "edgePadding", path, errors);
                return;
            }
        }

        private static void ValidateTooltip(TooltipTrigger trigger, Transform root, List<string> errors)
        {
            var data = new SerializedObject(trigger);
            var path = Path(trigger.transform, root);
            ValidateOverlayNumber(data, "displayDelay", path, errors);
            var overlay = OverlayReference<AnchoredOverlayElement>(data, "overlay");
            if (overlay == null)
            {
                errors.Add($"Tooltip overlay reference is missing: {path}.");
                return;
            }

            var overlayData = new SerializedObject(overlay);
            var content = OverlayReference<RectTransform>(overlayData, "content");
            if (content == null)
            {
                errors.Add($"Tooltip overlay content is missing: {path}.");
                return;
            }

            if (trigger.transform.IsChildOf(content))
            {
                errors.Add($"Tooltip target cannot belong to its overlay content: {path}.");
            }

            // Prefab 资源可能尚未位于宿主 Canvas 下，仅拒绝可证实的不匹配。
            var targetCanvas = trigger.GetComponentInParent<Canvas>(true);
            var overlayCanvas = overlay.GetComponentInParent<Canvas>(true);
            if (targetCanvas != null && overlayCanvas != null && targetCanvas.rootCanvas != overlayCanvas.rootCanvas)
            {
                errors.Add($"Tooltip target and overlay belong to different root Canvases: {path}.");
            }

            foreach (var graphic in content.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget || !graphic.enabled)
                {
                    continue;
                }

                if (TooltipRaycastBlocked(graphic.transform, content))
                {
                    continue;
                }

                errors.Add($"Tooltip Graphic can intercept hover; disable Raycast Target or use a blocking-disabled CanvasGroup: {Path(graphic.transform, root)}.");
            }
        }

        private static bool TooltipRaycastBlocked(Transform node, Transform content)
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

        private static void ValidateContextMenu(ContextMenuController menu, Transform root, List<string> errors)
        {
            var path = Path(menu.transform, root);
            var data = new SerializedObject(menu);
            var capacity = data.FindProperty("maxItems").intValue;
            if (capacity < 1)
            {
                errors.Add($"Context menu item capacity must be positive: {path}.");
            }

            var backView = OverlayReference<View>(data, "backNavigationView");
            if (backView != null && !menu.transform.IsChildOf(backView.transform))
            {
                errors.Add($"菜单的返回目标必须是所属层级中的 View：{path}。");
            }

            var overlay = menu.GetComponent<AnchoredOverlayElement>();
            if (overlay == null)
            {
                errors.Add($"Context menu requires AnchoredOverlayElement: {path}.");
                return;
            }

            var content = OverlayReference<RectTransform>(new SerializedObject(overlay), "content");
            if (content == null)
            {
                return;
            }

            var buttons = content.GetComponentsInChildren<Button>(true);
            if (buttons.Length > capacity)
            {
                errors.Add($"Context menu exceeds its item capacity: {path}.");
            }

            foreach (var button in buttons)
            {
                var relay = button.GetComponent<ContextMenuItemInput>();
                if (relay == null || !relay.enabled)
                {
                    errors.Add($"Menu Button needs enabled ContextMenuItemInput for native Cancel: {Path(button.transform, root)}.");
                }
            }

            foreach (var child in content.GetComponentsInChildren<ContextMenuController>(true))
            {
                if (child != menu)
                {
                    errors.Add($"Nested context menus are not supported by this controller: {Path(child.transform, root)}.");
                }
            }
        }

        private static void ValidateDismissArea(OverlayDismissArea area, Transform root, List<string> errors)
        {
            var path = Path(area.transform, root);
            var image = area.GetComponent<Image>();
            if (image == null)
            {
                errors.Add($"Overlay dismiss area needs an Image on the same object: {path}.");
            }

            if (area.GetComponent<AnchoredOverlayElement>() == null)
            {
                errors.Add($"Overlay dismiss area needs AnchoredOverlayElement on the same object: {path}.");
            }

            if (area.GetComponent<Selectable>() != null)
            {
                errors.Add($"Overlay background must not also be a Selectable: {path}.");
            }

            if (image != null && image.alphaHitTestMinimumThreshold > 0)
            {
                errors.Add($"Overlay background alpha hit testing can create holes in its input barrier: {path}.");
            }
        }

        private static T OverlayReference<T>(SerializedObject data, string property)
                    where T : UnityEngine.Object => data.FindProperty(property).objectReferenceValue as T;

        private static void ValidateOverlayNumber(SerializedObject data, string property, string path, List<string> errors)
        {
            var value = data.FindProperty(property).doubleValue;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            {
                errors.Add($"{property} must be finite and nonnegative: {path}.");
            }
        }
    }
}
