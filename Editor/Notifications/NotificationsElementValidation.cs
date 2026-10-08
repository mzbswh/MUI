using System.Collections.Generic;
using MUI.Editor;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Editor.Extensions
{
    [InitializeOnLoad]
    internal static class NotificationsElementValidation
    {
        static NotificationsElementValidation()
        {
            ViewContractValidator.RegisterElementValidator<NotificationElement>(Validate);
        }

        private static void Validate(NotificationElement element, View view, ICollection<string> errors)
        {
            var data = new SerializedObject(element);
            var path = AnimationUtility.CalculateTransformPath(element.transform, view.transform);
            var indicator = OverlayReference<GameObject>(data, "indicator");
            if (indicator == null || indicator.transform == element.transform || !indicator.transform.IsChildOf(element.transform))
            {
                errors.Add($"Indicator must be a strict child of the adapter: {path}.");
                return;
            }

            var message = OverlayReference<Text>(data, "message");
            if (message == null)
            {
                errors.Add($"Notification message Text is missing: {path}.");
            }
            else
            {
                ValidateOverlayChild(message.transform, indicator.transform, false, "Message", path, errors);
            }

            var button = OverlayReference<Button>(data, "dismissButton");
            if (button != null)
            {
                ValidateOverlayChild(button.transform, indicator.transform, false, "Dismiss button", path, errors);
            }
        }

        private static T OverlayReference<T>(SerializedObject data, string property)
                    where T : UnityEngine.Object => data.FindProperty(property).objectReferenceValue as T;

        private static void ValidateOverlayChild(Transform node,
                    Transform parent,
                    bool strict,
                    string label,
                    string path,
                    ICollection<string> errors)
        {
            if ((strict && node == parent) || !node.IsChildOf(parent))
            {
                errors.Add($"{label} must be {(strict ? "a strict child of" : "inside")} its indicator: {path}.");
            }
        }
    }
}
