using System;
using System.Collections.Generic;

namespace MUI
{
    public sealed partial class TreeList<T>
    {
        private Snapshot BuildSnapshot(IEnumerable<TreeNode<T>> nodes, bool preserveExpansion)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            var input = new List<TreeNode<T>>();
            var keys = new List<object>();
            var inputIndices = new Dictionary<object, int>();
            foreach (var node in nodes)
            {
                if (node == null || input.Count == Capacity)
                {
                    throw new InvalidOperationException("Tree nodes must be non-null and fit the configured node capacity.");
                }

                var key = itemKey(node.Item);
                if (key == null || !inputIndices.TryAdd(key, input.Count))
                {
                    throw new InvalidOperationException("Tree item keys must be non-null, stable and unique.");
                }

                input.Add(node);
                keys.Add(key);
            }

            var children = new List<int>[input.Count];
            var roots = new List<int>();
            for (var index = 0; index < input.Count; ++index)
            {
                var parentKey = input[index].ParentKey;
                if (parentKey == null)
                {
                    roots.Add(index);
                    continue;
                }

                if (!inputIndices.TryGetValue(parentKey, out var parent) || parent == index)
                {
                    throw new InvalidOperationException("Tree parents must exist and cannot reference their own node.");
                }

                if (children[parent] == null)
                {
                    children[parent] = new List<int>();
                }

                children[parent].Add(index);
            }

            var ordered = new List<Entry>(input.Count);
            var orderedIndices = new Dictionary<object, int>();
            var expanded = new List<bool>(input.Count);
            var visited = new bool[input.Count];
            var stack = new Stack<TraversalFrame>();
            for (var index = roots.Count - 1; index >= 0; --index)
            {
                stack.Push(new TraversalFrame(roots[index], -1, 0));
            }

            while (stack.Count != 0)
            {
                var frame = stack.Pop();
                if (frame.ExitPosition >= 0)
                {
                    // 子树在先序数组中连续，用结束位置在折叠时一次跳过全部后代。
                    ordered[frame.ExitPosition].End = ordered.Count;
                    continue;
                }

                if (frame.Depth > MaxDepth || visited[frame.Source])
                {
                    throw new InvalidOperationException("Tree must be acyclic and fit the configured depth limit.");
                }

                visited[frame.Source] = true;
                var position = ordered.Count;
                var node = input[frame.Source];
                var key = keys[frame.Source];
                orderedIndices.Add(key, position);
                ordered.Add(new Entry { Key = key, Node = node, Parent = frame.Parent, Depth = frame.Depth });
                expanded.Add(preserveExpansion && snapshot.Indices.TryGetValue(key, out var oldIndex)
                    ? snapshot.Expanded[oldIndex] : node.InitiallyExpanded);
                stack.Push(new TraversalFrame(-1, -1, 0, position));
                var descendants = children[frame.Source];
                if (descendants != null)
                {
                    for (var index = descendants.Count - 1; index >= 0; --index)
                    {
                        stack.Push(new TraversalFrame(descendants[index], position, frame.Depth + 1));
                    }
                }
            }

            // 每个节点只有一个父级；不从任意根可达的剩余分量必然含有父链环。
            if (ordered.Count != input.Count)
            {
                throw new InvalidOperationException("Tree contains a parent cycle disconnected from its roots.");
            }

            return new Snapshot(ordered.ToArray(), orderedIndices, expanded.ToArray());
        }

        private readonly struct TraversalFrame
        {
            internal TraversalFrame(int source, int parent, int depth, int exitPosition = -1)
            {
                Source = source;
                Parent = parent;
                Depth = depth;
                ExitPosition = exitPosition;
            }

            internal int Source
            {
                get;
            }

            internal int Parent
            {
                get;
            }

            internal int Depth
            {
                get;
            }

            internal int ExitPosition
            {
                get;
            }
        }
    }
}
