using System;
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
                    errors.Add(L.Format("editor.ViewContractValidator.Overlays.955db50acf", path));
                }

                if (content != null)
                {
                    if (content.localScale != Vector3.one || Quaternion.Angle(content.localRotation, Quaternion.identity) > 0.01f)
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.Overlays.fedcdac7d9", path));
                    }

                    var layout = element.GetComponent<LayoutGroup>();
                    var layoutElement = content.GetComponent<LayoutElement>();
                    if (layout != null && layout.enabled && (layoutElement == null || !layoutElement.ignoreLayout))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.Overlays.62f7957076", path));
                    }
                }

                var side = data.FindProperty("placement").intValue;
                if (!Enum.IsDefined(typeof(OverlayPlacement), side))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.Overlays.4e3c940dd7", path));
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
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.c1585ec208", path));
                return;
            }

            var overlayData = new SerializedObject(overlay);
            var content = OverlayReference<RectTransform>(overlayData, "content");
            if (content == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.7000c2e0e2", path));
                return;
            }

            if (trigger.transform.IsChildOf(content))
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.e5c8bbe300", path));
            }

            // Prefab 资源可能尚未位于宿主 Canvas 下，仅拒绝可证实的不匹配。
            var targetCanvas = trigger.GetComponentInParent<Canvas>(true);
            var overlayCanvas = overlay.GetComponentInParent<Canvas>(true);
            if (targetCanvas != null && overlayCanvas != null && targetCanvas.rootCanvas != overlayCanvas.rootCanvas)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.1df5f4ddfd", path));
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

                errors.Add(L.Format("editor.ViewContractValidator.Overlays.b7251ed46c", Path(graphic.transform, root)));
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
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.d8ce0ad48e", path));
            }

            var backView = OverlayReference<View>(data, "backNavigationView");
            if (backView != null && !menu.transform.IsChildOf(backView.transform))
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.5d06e12504", path));
            }

            var overlay = menu.GetComponent<AnchoredOverlayElement>();
            if (overlay == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.e67dc06d2e", path));
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
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.6e7b5fd142", path));
            }

            foreach (var button in buttons)
            {
                var relay = button.GetComponent<ContextMenuItemInput>();
                if (relay == null || !relay.enabled)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.Overlays.5698adc887", Path(button.transform, root)));
                }
            }

            foreach (var child in content.GetComponentsInChildren<ContextMenuController>(true))
            {
                if (child != menu)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.Overlays.f1f55a0471", Path(child.transform, root)));
                }
            }
        }

        private static void ValidateDismissArea(OverlayDismissArea area, Transform root, List<string> errors)
        {
            var path = Path(area.transform, root);
            var image = area.GetComponent<Image>();
            if (image == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.b7542db637", path));
            }

            if (area.GetComponent<AnchoredOverlayElement>() == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.1cc729e41f", path));
            }

            if (area.GetComponent<Selectable>() != null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.6969df3471", path));
            }

            if (image != null && image.alphaHitTestMinimumThreshold > 0)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.b49956a282", path));
            }
        }

        private static T OverlayReference<T>(SerializedObject data, string property)
                    where T : UnityEngine.Object => data.FindProperty(property).objectReferenceValue as T;

        private static void ValidateOverlayNumber(SerializedObject data, string property, string path, List<string> errors)
        {
            var value = data.FindProperty(property).doubleValue;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            {
                errors.Add(L.Format("editor.ViewContractValidator.Overlays.7b287a6832", property, path));
            }
        }
    }
}
