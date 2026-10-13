using System;
using System.Collections.Generic;
using MUI.UGUI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        public static IReadOnlyList<string> ValidateAccessibility(View view, BindingManifest manifest = null)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var errors = new List<string>();
            var elements = new List<Element>();
            Collect(view.transform, view.transform, elements, errors, true);
            foreach (var element in elements)
            {
                try
                {
                    if (AccessibilityTree.IsExcluded(element, view))
                    {
                        continue;
                    }

                    var role = element.SemanticRole;
                    if (!Enum.IsDefined(typeof(AccessibilityRole), role))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.Accessibility.852c506d7d", Path(element.transform, view.transform)));
                        continue;
                    }

                    var needsLabel = role == AccessibilityRole.Button || role == AccessibilityRole.Toggle || role == AccessibilityRole.Slider || role == AccessibilityRole.TextInput || role == AccessibilityRole.Dropdown || role == AccessibilityRole.Tab || role == AccessibilityRole.Image;
                    var bound = false;
                    if (manifest != null)
                    {
                        foreach (var entry in manifest.Entries)
                        {
                            if (entry.Kind == BindingEntryKind.Property && entry.Mode != BindingMode.OneWayToSource && entry.ElementName == element.Name && entry.ElementType.IsInstanceOfType(element) && entry.TargetProperty == nameof(Element.AccessibilityLabel))
                            {
                                bound = true;
                                break;
                            }
                        }
                    }

                    if (needsLabel && !bound && string.IsNullOrWhiteSpace(element.AccessibilityLabel))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.Accessibility.2998c776ae", role, Path(element.transform, view.transform)));
                    }

                    const AccessibilityState known = AccessibilityState.Disabled | AccessibilityState.Selected | AccessibilityState.Checked | AccessibilityState.Expanded | AccessibilityState.ReadOnly | AccessibilityState.Busy | AccessibilityState.Invalid;
                    if ((element.SemanticState & ~known) != 0)
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.Accessibility.27a823a589", Path(element.transform, view.transform)));
                    }
                }
                catch (Exception error)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.Accessibility.538b93e816", Path(element.transform, view.transform), error.Message));
                }
            }

            return errors;
        }
    }
}
