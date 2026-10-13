using System.Collections.Generic;
using MUI.Editor;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

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
                errors.Add(L.Format("editor.NotificationsElementValidation.c6205dce35", path));
                return;
            }

            var message = OverlayReference<Text>(data, "message");
            if (message == null)
            {
                errors.Add(L.Format("editor.NotificationsElementValidation.143a5b79bd", path));
            }
            else
            {
                ValidateOverlayChild(message.transform, indicator.transform, false, "Message", path, errors);
            }

            var button = OverlayReference<Button>(data, "dismissButton");
            if (button != null)
            {
                ValidateOverlayChild(button.transform, indicator.transform, false, L.Get("editor.NotificationsElementValidation.7b34326d17"), path, errors);
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
                errors.Add(L.Format("editor.NotificationsElementValidation.71eae1a677", label, (strict ? L.Get("editor.NotificationsElementValidation.ee5ca91704") : "inside"), path));
            }
        }
    }
}
