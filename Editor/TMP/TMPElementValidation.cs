using System.Collections.Generic;
using MUI.Editor;
using MUI.UGUI;
using TMPro;
using UnityEditor;
using UnityEngine;

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
                Report(element, view, errors, "TMP text requires TextMeshProUGUI on the same object");
            }
        }

        private static void ValidateInput(TMPInputFieldElement element, View view, ICollection<string> errors)
        {
            var input = element.GetComponent<TMP_InputField>();
            if (input == null)
            {
                Report(element, view, errors, "TMP input requires TMP_InputField");
                return;
            }

            var viewport = input.textViewport;
            var text = input.textComponent;
            if (viewport == null)
            {
                Report(element, view, errors, "TMP input text viewport is missing");
            }
            else if (!viewport.IsChildOf(input.transform))
            {
                Report(element, view, errors, "TMP input text viewport must belong to its hierarchy");
            }

            if (viewport != null && viewport.GetComponentInParent<View>(true) != view)
            {
                Report(element, view, errors, "TMP 输入框视口引用不能跨越 View 边界");
            }

            if (text == null)
            {
                Report(element, view, errors, "TMP input text component is missing");
            }
            else
            {
                if (!(text is TextMeshProUGUI))
                {
                    Report(element, view, errors, "TMP input requires a uGUI text component");
                }

                if (text.GetComponentInParent<View>(true) != view)
                {
                    Report(element, view, errors, "TMP 输入框文字引用不能跨越 View 边界");
                }

                if (viewport != null && (text.transform == viewport || !text.transform.IsChildOf(viewport)))
                {
                    Report(element, view, errors, "TMP input text must be a descendant of its viewport");
                }
            }

            if (input.placeholder != null && viewport != null && !input.placeholder.transform.IsChildOf(viewport))
            {
                Report(element, view, errors, "TMP input placeholder must belong to its viewport");
            }

            if (input.placeholder != null && text != null && input.placeholder == text)
            {
                Report(element, view, errors, "TMP input placeholder cannot also be the editable text");
            }

            if (input.placeholder != null && input.placeholder.GetComponentInParent<View>(true) != view)
            {
                Report(element, view, errors, "TMP 输入框占位引用不能跨越 View 边界");
            }
        }

        private static void ValidateDropdown(TMPDropdownElement element, View view, ICollection<string> errors)
        {
            var dropdown = element.GetComponent<TMP_Dropdown>();
            if (dropdown == null)
            {
                Report(element, view, errors, "TMP dropdown requires TMP_Dropdown");
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
