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

        public Element Source
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

            foreach (var element in root.GetComponentsInChildren<Element>(true))
            {
                if (element == null || !element.IsAlive || !element.isActiveAndEnabled || !element.gameObject.activeInHierarchy || element.AccessibilityHidden)
                {
                    continue;
                }

                if (IsExcluded(element, root))
                {
                    continue;
                }

                var owner = element.GetComponentInParent<View>(true);
                if (owner != null && (!owner.IsAlive || !owner.IsVisible))
                {
                    continue;
                }

                ordered.Add((new AccessibilityNode(element), ordered.Count));
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
        public static bool IsExcluded(Element element, View root)
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

            if (element.AccessibilityHidden)
            {
                return true;
            }

            if (element.transform == root.transform)
            {
                return false;
            }

            var passive = element.SemanticRole == AccessibilityRole.Text || element.SemanticRole == AccessibilityRole.Image;
            for (var node = element.transform.parent; node != null; node = node.parent)
            {
                foreach (var ancestor in node.GetComponents<Element>())
                {
                    if (ancestor == null || !ancestor.IsAlive)
                    {
                        continue;
                    }

                    if (ancestor.AccessibilityHidden)
                    {
                        return true;
                    }

                    // 子文本与图片属于控件的语义标签或值，尤其不能
                    // 将输入内容作为独立可读节点暴露。
                    if (passive && (ancestor.SemanticRole == AccessibilityRole.Button || ancestor.SemanticRole == AccessibilityRole.Toggle || ancestor.SemanticRole == AccessibilityRole.Slider || ancestor.SemanticRole == AccessibilityRole.TextInput || ancestor.SemanticRole == AccessibilityRole.Dropdown || ancestor.SemanticRole == AccessibilityRole.Tab))
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
