using System.Collections.Generic;
using MUI.Editor;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Editor.Extensions
{
    [InitializeOnLoad]
    internal static class LoadingElementValidation
    {
        static LoadingElementValidation()
        {
            ViewContractValidator.RegisterElementValidator<LoadingElement>(Validate);
        }

        private static void Validate(LoadingElement element, View view, ICollection<string> errors)
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
            var progress = OverlayReference<Image>(data, "progress");
            var indeterminate = OverlayReference<GameObject>(data, "indeterminate");
            if (message != null)
            {
                ValidateOverlayChild(message.transform, indicator.transform, true, "Message", path, errors);
            }

            if (progress != null)
            {
                ValidateOverlayChild(progress.transform, indicator.transform, true, "Progress", path, errors);
                if (progress.type != Image.Type.Filled)
                {
                    errors.Add($"Loading progress requires a Filled Image: {path}.");
                }
            }

            if (indeterminate != null)
            {
                ValidateOverlayChild(indeterminate.transform, indicator.transform, true, "Indeterminate", path, errors);
            }

            if (progress != null && indeterminate != null && (progress.transform.IsChildOf(indeterminate.transform) || indeterminate.transform.IsChildOf(progress.transform)))
            {
                errors.Add($"Loading progress and indeterminate nodes must be independent branches: {path}.");
            }

            if (message != null && ((progress != null && message.transform.IsChildOf(progress.transform)) || (indeterminate != null && message.transform.IsChildOf(indeterminate.transform))))
            {
                errors.Add($"Loading message cannot be inside a conditionally hidden progress branch: {path}.");
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
