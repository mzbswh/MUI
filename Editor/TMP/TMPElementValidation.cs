using System.Collections.Generic;
using MUI.Editor;
using MUI.UGUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.TMP.Editor
{
    [InitializeOnLoad]
    internal static class TMPElementValidation
    {
        static TMPElementValidation()
        {
            ViewContractValidator.RegisterElementValidator<TMPTextElement>(ValidateText);
            ViewContractValidator.RegisterElementValidator<TMPInputFieldElement>(ValidateInput);
            ViewContractValidator.RegisterElementValidator<TMPDropdownElement>(ValidateDropdown);
        }

        private static void ValidateText(TMPTextElement element, View view, ICollection<string> errors)
        {
            if (element.GetComponent<TextMeshProUGUI>() == null)
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.5e17708354"));
            }
        }

        private static void ValidateInput(TMPInputFieldElement element, View view, ICollection<string> errors)
        {
            var input = element.GetComponent<TMP_InputField>();
            if (input == null)
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.4969873d55"));
                return;
            }

            var viewport = input.textViewport;
            var text = input.textComponent;
            if (viewport == null)
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.1d8c5c36f6"));
            }
            else if (!viewport.IsChildOf(input.transform))
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.1df8e0ccfc"));
            }

            if (viewport != null && viewport.GetComponentInParent<View>(true) != view)
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.2de1131ac8"));
            }

            if (text == null)
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.e476e0fbaf"));
            }
            else
            {
                if (!(text is TextMeshProUGUI))
                {
                    Report(element, view, errors, L.Get("editor.TMPElementValidation.fd176822a0"));
                }

                if (text.GetComponentInParent<View>(true) != view)
                {
                    Report(element, view, errors, L.Get("editor.TMPElementValidation.fbfd3003d5"));
                }

                if (viewport != null && (text.transform == viewport || !text.transform.IsChildOf(viewport)))
                {
                    Report(element, view, errors, L.Get("editor.TMPElementValidation.1a3adba300"));
                }
            }

            if (input.placeholder != null && viewport != null && !input.placeholder.transform.IsChildOf(viewport))
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.8b054a99fa"));
            }

            if (input.placeholder != null && text != null && input.placeholder == text)
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.1ca7e4bdba"));
            }

            if (input.placeholder != null && input.placeholder.GetComponentInParent<View>(true) != view)
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.1f32665973"));
            }
        }

        private static void ValidateDropdown(TMPDropdownElement element, View view, ICollection<string> errors)
        {
            var dropdown = element.GetComponent<TMP_Dropdown>();
            if (dropdown == null)
            {
                Report(element, view, errors, L.Get("editor.TMPElementValidation.e88edcb824"));
                return;
            }

            DropdownTemplateValidation.Validate(dropdown.template, dropdown.itemText, dropdown.itemImage, ElementPath(element, view), errors);
        }

        private static void Report(Element element, View view, ICollection<string> errors, string message) => errors.Add(message + ": " + ElementPath(element, view) + ".");

        private static string ElementPath(Element element, View view)
        {
            var segments = new List<string>();
            for (var node = element.transform; node != null; node = node.parent)
            {
                segments.Add(node.name + "[" + node.GetSiblingIndex() + "]");
                if (node == view.transform)
                {
                    break;
                }
            }

            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
