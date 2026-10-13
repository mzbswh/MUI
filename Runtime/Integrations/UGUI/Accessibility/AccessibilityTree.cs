using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace MUI.UGUI
{
    public readonly struct AccessibilityNode
    {
        internal AccessibilityNode(Element source)
        {
            Source = source;
            Role = source.SemanticRole;
            State = source.SemanticState;
            Label = source.AccessibilityLabel;
            Description = source.AccessibilityDescription;
            Value = source.AccessibilityValue;
            Order = source.AccessibilityOrder;
        }

        internal AccessibilityNode(Component source, AccessibilityRole role, AccessibilityState state, string label)
        {
            Source = source;
            Role = role;
            State = state;
            Label = label;
            Description = string.Empty;
            Value = string.Empty;
            Order = 0;
        }

        public Component Source
        {
            get;
        }

        public AccessibilityRole Role
        {
            get;
        }

        public AccessibilityState State
        {
            get;
        }

        public string Label
        {
            get;
        }

        public string Description
        {
            get;
        }

        public string Value
        {
            get;
        }

        public int Order
        {
            get;
        }
    }

    public static class AccessibilityTree
    {
        private static readonly Dictionary<Type, Func<Component, AccessibilityRole>> roles = new Dictionary<Type, Func<Component, AccessibilityRole>>();
        private static readonly Dictionary<Type, Func<Component, string>> labels = new Dictionary<Type, Func<Component, string>>();

        /// <summary>可选文本后端提供语义读取，不需要每个文本节点挂包装组件。</summary>
        public static void Register<T>(AccessibilityRole role, Func<T, string> label) where T : Component
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            roles[typeof(T)] = _ => role;
            labels[typeof(T)] = component => label((T)component);
        }

        private static AccessibilityRole RoleOf(Component component)
        {
            if (component is IAccessibleElement semantic) return semantic.SemanticRole;
            for (var type = component.GetType(); type != null; type = type.BaseType)
                if (roles.TryGetValue(type, out var role)) return role(component);
            if (component is InputField) return AccessibilityRole.TextInput;
            if (component is Dropdown) return AccessibilityRole.Dropdown;
            if (component is Toggle) return AccessibilityRole.Toggle;
            if (component is Slider || component is Scrollbar) return AccessibilityRole.Slider;
            if (component is Button) return AccessibilityRole.Button;
            if (component is Text) return AccessibilityRole.Text;
            if (component is Graphic) return AccessibilityRole.Image;
            return AccessibilityRole.Generic;
        }

        private static string LabelOf(Component component)
        {
            if (component is IAccessibleElement semantic) return semantic.AccessibilityLabel;
            for (var type = component.GetType(); type != null; type = type.BaseType)
                if (labels.TryGetValue(type, out var label)) return label(component);
            if (component is Text text) return text.text;
            // Input contents (especially passwords) are never used as labels.
            if (RoleOf(component) == AccessibilityRole.TextInput) return string.Empty;
            if (component is Selectable)
                foreach (var graphic in component.GetComponentsInChildren<Graphic>(true))
                    if (RoleOf(graphic) == AccessibilityRole.Text) return LabelOf(graphic);
            return string.Empty;
        }

        private static AccessibilityState StateOf(Component component)
        {
            if (component is IAccessibleElement semantic) return semantic.SemanticState;
            var state = AccessibilityState.None;
            var view = component.GetComponentInParent<View>(true);
            if (component is Selectable selectable && (!selectable.IsInteractable() || view != null && !view.IsInputEnabled)) state |= AccessibilityState.Disabled;
            if (component is Toggle toggle && toggle.isOn) state |= AccessibilityState.Checked;
            if (component is InputField input && input.readOnly) state |= AccessibilityState.ReadOnly;
            return state;
        }

        /// <summary>拉取包含可见嵌套 View 的快照；来源对象为借用，使用前必须重新校验。</summary>
        public static IReadOnlyList<AccessibilityNode> Capture(View root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var ordered = new List<(AccessibilityNode node, int index)>();
            if (!root.IsAlive || !root.IsVisible)
            {
                return Array.Empty<AccessibilityNode>();
            }

            foreach (var element in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (element == null || !element.isActiveAndEnabled || !element.gameObject.activeInHierarchy || element is IAccessibleElement semantic && (!semantic.IsAlive || semantic.AccessibilityHidden))
                {
                    continue;
                }

                if (!(element is IAccessibleElement) && !(element is Selectable) && !(element is Graphic)) continue;
                if (!(element is Element) && element.GetComponent<Element>() != null) continue;
                if (element is Graphic && element.GetComponent<Selectable>() != null) continue;
                if (IsExcluded(element, root))
                {
                    continue;
                }

                var owner = element.GetComponentInParent<View>(true);
                if (owner != null && (!owner.IsAlive || !owner.IsVisible))
                {
                    continue;
                }

                var snapshot = element is Element custom ? new AccessibilityNode(custom) :
                    new AccessibilityNode(element, RoleOf(element), StateOf(element), LabelOf(element));
                ordered.Add((snapshot, ordered.Count));
            }

            ordered.Sort((a, b) =>
            {
                var order = a.node.Order.CompareTo(b.node.Order);
                return order != 0 ? order : a.index.CompareTo(b.index);
            });
            var result = new AccessibilityNode[ordered.Count];
            for (var i = 0; i < result.Length; ++i)
            {
                result[i] = ordered[i].node;
            }

            return Array.AsReadOnly(result);
        }

        /// <summary>仅排除语义节点，检查未激活 Prefab 时也可使用。</summary>
        public static bool IsExcluded(Component element, View root)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (!element.transform.IsChildOf(root.transform))
            {
                throw new ArgumentException("Element is outside the supplied View subtree.", nameof(element));
            }

            if (element is IAccessibleElement semantic && semantic.AccessibilityHidden)
            {
                return true;
            }

            if (element.transform == root.transform)
            {
                return false;
            }

            var passive = RoleOf(element) == AccessibilityRole.Text || RoleOf(element) == AccessibilityRole.Image;
            for (var node = element.transform.parent; node != null; node = node.parent)
            {
                foreach (var ancestor in node.GetComponents<MonoBehaviour>())
                {
                    if (ancestor == null || ancestor is IAccessibleElement accessible && !accessible.IsAlive)
                    {
                        continue;
                    }

                    if (ancestor is IAccessibleElement hidden && hidden.AccessibilityHidden)
                    {
                        return true;
                    }

                    // 子文本与图片属于控件的语义标签或值，尤其不能
                    // 将输入内容作为独立可读节点暴露。
                    if (passive && (RoleOf(ancestor) == AccessibilityRole.Button || RoleOf(ancestor) == AccessibilityRole.Toggle || RoleOf(ancestor) == AccessibilityRole.Slider || RoleOf(ancestor) == AccessibilityRole.TextInput || RoleOf(ancestor) == AccessibilityRole.Dropdown || RoleOf(ancestor) == AccessibilityRole.Tab))
                    {
                        return true;
                    }
                }

                if (node == root.transform)
                {
                    break;
                }
            }

            return false;
        }
    }
}
