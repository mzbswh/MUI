using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    /// <summary>读取创作数据，不初始化 Element 或挂接运行时监听器。</summary>
    public static partial class ViewContractValidator
    {
        /// <summary>按校验器相同的 View/Element 边界定位目标，返回所有匹配项而不猜测重名对象。</summary>
        internal static IReadOnlyList<Element> FindBindingTargets(View view, BindingEntry entry)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var elements = new List<Element>();
            Collect(view.transform, view.transform, elements, new List<string>(), true);
            var matches = new List<Element>();
            foreach (var element in elements)
            {
                if (element != null && element.Name == entry.ElementName && entry.ElementType.IsInstanceOfType(element))
                {
                    matches.Add(element);
                }
            }

            return matches;
        }

        public static IReadOnlyList<string> Validate(View view, BindingManifest manifest)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var errors = new List<string>();
            var elements = new List<Element>();
            Collect(view.transform, view.transform, elements, errors, true);
            ValidateStructure(view, elements, errors);
            var scrollOwners = view.GetComponentsInChildren<ScrollRect>(true);
            var targetWriters = new Dictionary<(Element element, string property), string>();
            var sourceWriters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in manifest.Entries)
            {
                var count = 0;
                var matches = new List<string>();
                Element matched = null;
                foreach (var element in elements)
                {
                    if (element.Name == entry.ElementName && entry.ElementType.IsInstanceOfType(element))
                    {
                        count++;
                        matched = element;
                        matches.Add(Path(element.transform, view.transform));
                    }
                }

                if (count == 0)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.a2c70aba3a", entry.Source, entry.ElementName, entry.ElementType.Name));
                }
                else if (count > 1)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.7d3464be21", entry.Source, entry.ElementName, entry.ElementType.Name, string.Join(", ", matches)));
                }

                if (count == 1 && matched is ImageElement && entry.Kind == BindingEntryKind.Property && entry.TargetProperty == nameof(ImageElement.FillAmount) && entry.Mode != BindingMode.OneWayToSource)
                {
                    var image = matched.GetComponent<Image>();
                    if (image == null || (image.type != Image.Type.Filled && !HasImageTypeWriter(matched, manifest)))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.654d03c722", Path(matched.transform, view.transform)));
                    }
                }

                if (entry.Kind == BindingEntryKind.Command)
                {
                    if (count == 1)
                    {
                        var commandTarget = (matched, entry.InteractableProperty);
                        if (targetWriters.TryGetValue(commandTarget, out var previous))
                        {
                            errors.Add(L.Format("editor.ViewContractValidator.adc3272193", Path(matched.transform, view.transform), entry.InteractableProperty, previous, entry.Source));
                        }
                        else
                        {
                            targetWriters.Add(commandTarget, L.Get("editor.ViewContractValidator.9e0b272fe8") + entry.Source);
                        }
                    }

                    continue;
                }

                if (count == 1 && matched is ScrollbarElement scrollbar)
                {
                    ValidateScrollbarWriter(scrollbar, entry, scrollOwners, view, errors);
                }

                var targetProperty = entry.TargetProperty;
                if (count == 1 && matched is IBindingPropertyPolicy policy)
                {
                    try
                    {
                        targetProperty = policy.GetBindingWriteTarget(entry.TargetProperty, entry.Mode);
                        if (string.IsNullOrEmpty(targetProperty))
                        {
                            throw new InvalidOperationException(L.Get("editor.ViewContractValidator.6853150e20"));
                        }
                    }
                    catch (Exception error)
                    {
                        errors.Add($"{Path(matched.transform, view.transform)}.{entry.TargetProperty}: {error.Message}");
                        continue;
                    }
                }

                if ((entry.Mode == BindingMode.TwoWay || entry.Mode == BindingMode.OneWayToSource) && count == 1)
                {
                    var writer = Path(matched.transform, view.transform) + "." + entry.TargetProperty;
                    if (sourceWriters.TryGetValue(entry.Source, out var previous))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.36fab33a6d", entry.Source, previous, writer));
                    }
                    else
                    {
                        sourceWriters.Add(entry.Source, writer);
                    }
                }

                if (entry.Mode != BindingMode.OneWayToSource && count == 1)
                {
                    // 资源键与直接资源属性共享同一个原生写入位置，不能作为两条独立绑定。
                    var target = (matched, targetProperty);
                    if (targetWriters.TryGetValue(target, out var previous))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.81efe73d85", Path(matched.transform, view.transform), entry.TargetProperty, previous, entry.Source));
                    }
                    else
                    {
                        targetWriters.Add(target, entry.Source);
                    }
                }
            }

            return errors;
        }

        /// <summary>动态类型绑定的实际值由模型决定，不能仅凭 Prefab 初值拒绝填充绑定。</summary>
        private static bool HasImageTypeWriter(Element element, BindingManifest manifest)
        {
            foreach (var entry in manifest.Entries)
            {
                if (entry.Kind == BindingEntryKind.Property && entry.Mode != BindingMode.OneWayToSource &&
                    entry.TargetProperty == nameof(ImageElement.ImageType) && entry.ElementName == element.Name &&
                    entry.ElementType.IsInstanceOfType(element))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>在没有生成业务契约时校验预制 View 结构。</summary>
        public static IReadOnlyList<string> ValidateStructure(View view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var errors = new List<string>();
            var elements = new List<Element>();
            Collect(view.transform, view.transform, elements, errors, true);
            ValidateStructure(view, elements, errors);
            return errors;
        }

        private static void ValidateStructure(View view, List<Element> elements, List<string> errors)
        {
            if (!Enum.IsDefined(typeof(ViewHiddenMode), view.HiddenMode) ||
                (view.HiddenMode == ViewHiddenMode.DeactivateContent && (view.HiddenContentRoot == null ||
                    view.HiddenContentRoot == view.gameObject || !view.HiddenContentRoot.transform.IsChildOf(view.transform))))
            {
                errors.Add(L.Get("policy.hidden.invalid"));
            }

            var sortingTargets = new HashSet<Component>();
            if (view.RenderOrderTargets != null)
            {
                foreach (var binding in view.RenderOrderTargets)
                {
                    if (binding == null || binding.Target == null ||
                        (!(binding.Target is Canvas) && !(binding.Target is Renderer)) ||
                        binding.Target.gameObject == view.gameObject || !binding.Target.transform.IsChildOf(view.transform) ||
                        binding.Offset < 2 || !sortingTargets.Add(binding.Target))
                    {
                        errors.Add(L.Get("sorting.target.invalid"));
                    }
                }
            }
            ValidateTransitions(view, errors);
            var keys = new Dictionary<string, Element>(StringComparer.Ordinal);
            var validators = SnapshotElementValidators();
            foreach (var element in elements)
            {
                if (string.IsNullOrWhiteSpace(element.Name))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.db2b82af20", Path(element.transform, view.transform)));
                }

                var key = element.Name + "\n" + element.GetType().AssemblyQualifiedName;
                if (keys.TryGetValue(key, out var previousElement))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.e901f2c3ba", element.GetType().Name, Path(previousElement.transform, view.transform), Path(element.transform, view.transform)));
                }
                else
                {
                    keys.Add(key, element);
                }

                if (element is DropdownElement)
                {
                    ValidateDropdown(element, view.transform, errors);
                }

                if (element is InputFieldElement)
                {
                    ValidateInputField(element, view, errors);
                }

                if (element is IInputFieldElement input && !Enum.IsDefined(typeof(TextInputCommitMode), input.CommitMode))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.70a6c57f8d", Path(element.transform, view.transform)));
                }

                if (element is ScrollRectElement)
                {
                    ValidateScroll(element.GetComponent<ScrollRect>(), element.transform, view.transform, errors);
                }

                if (element is ScrollbarElement)
                {
                    ValidateScrollbar(element, view, errors);
                }

                if (element is VirtualListElement)
                {
                    ValidateVirtualList(element, view.transform, errors);
                }

                if (element is SlotListElement)
                {
                    ValidateSlotList(element, view.transform, errors);
                }
                else if (element is RecyclingListElement)
                {
                    ValidateRecyclingList(element, view.transform, errors);
                }

                ValidateOverlayElement(element, view.transform, errors);
                ValidateExtensions(element, view, errors, validators);
            }
        }

        private static void ValidateScroll(ScrollRect scroll, Transform element, Transform root, List<string> errors)
        {
            var path = Path(element, root);
            if (scroll == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.eb31327ed9", path));
                return;
            }

            if (scroll.content == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.fd6da981ad", path));
                return;
            }

            var viewport = scroll.viewport == null ? scroll.transform as RectTransform : scroll.viewport;
            if (viewport == null || !viewport.IsChildOf(scroll.transform))
            {
                errors.Add(L.Format("editor.ViewContractValidator.71f7f7caaa", path));
            }
            else if (scroll.content == viewport || !scroll.content.IsChildOf(viewport))
            {
                errors.Add(L.Format("editor.ViewContractValidator.cb101437c2", path));
            }
        }

        private static void ValidateDropdown(Element element, Transform root, List<string> errors)
        {
            var dropdown = element.GetComponent<Dropdown>();
            if (dropdown == null)
            {
                errors.Add(L.Format("editor.ViewContractValidator.1c85e45608", Path(element.transform, root)));
                return;
            }

            DropdownTemplateValidation.Validate(dropdown.template, dropdown.itemText, dropdown.itemImage, Path(element.transform, root), errors);
        }

        private static string Path(Transform node, Transform root)
        {
            var segments = new List<string>();
            while (node != null)
            {
                segments.Add(node.name + "[" + node.GetSiblingIndex() + "]");
                if (node == root)
                {
                    break;
                }

                node = node.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static void Collect(Transform node, Transform viewRoot, List<Element> elements, List<string> errors, bool root)
        {
            if (!root && node.GetComponent<View>() != null)
            {
                return;
            }

            var boundary = false;
            foreach (var component in node.GetComponents<MonoBehaviour>())
            {
                if (component == null)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.8fb811d725", Path(node, viewRoot)));
                    continue;
                }

                if (component is IElementBoundary)
                {
                    boundary = true;
                }

                if (component is Element element)
                {
                    elements.Add(element);
                }

                if (component is TooltipTrigger tooltip)
                {
                    ValidateTooltip(tooltip, viewRoot, errors);
                }

                if (component is ContextMenuController menu)
                {
                    ValidateContextMenu(menu, viewRoot, errors);
                }

                if (component is OverlayDismissArea area)
                {
                    ValidateDismissArea(area, viewRoot, errors);
                }
            }

            foreach (var group in node.GetComponents<CanvasGroup>())
            {
                if (group.ignoreParentGroups)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.0961434628", Path(node, viewRoot)));
                }
            }

            if (boundary)
            {
                return;
            }

            for (var i = 0; i < node.childCount; ++i)
            {
                Collect(node.GetChild(i), viewRoot, elements, errors, false);
            }
        }
    }
}
