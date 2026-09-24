using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>普通虚拟条目承载多层树，检查祖先展开、后代状态保留与稳定键定位。</summary>
        private static async Task DemonstrateTreeListAsync(PageViewModel model, VirtualListElement list)
        {
            var nodes = new List<TreeNode<VirtualListItem>>();
            for (var rootIndex = 0; rootIndex < 20; ++rootIndex)
            {
                var rootKey = "root:" + rootIndex;
                AddTreeNode(nodes, rootKey, null, 0, expanded: true);
                for (var branchIndex = 0; branchIndex < 5; ++branchIndex)
                {
                    var branchKey = "branch:" + rootIndex + ":" + branchIndex;
                    AddTreeNode(nodes, branchKey, rootKey, 1);
                    for (var leafIndex = 0; leafIndex < 20; ++leafIndex)
                    {
                        AddTreeNode(nodes, "leaf:" + rootIndex + ":" + branchIndex + ":" + leafIndex, branchKey, 2);
                    }
                }
            }

            var tree = new TreeList<VirtualListItem>(item => item.Key, maxNodes: 3000, maxDepth: 8);
            tree.Reset(nodes);
            model.VirtualItems = tree;
            await list.PendingChange;
            const string target = "leaf:7:2:13";
            Debug.Log($"MUI Tree initial: total={tree.TotalNodeCount}; visible={tree.Count}; " +
                $"targetIndex={tree.GetVisibleIndex(target)}; depth={tree.GetDepth(target)}; cells={list.MaterializedCount}");

            tree.ExpandAncestors(target);
            await list.PendingChange;
            var reveal = await list.ScrollToKeyAsync(target, focus: true);
            model.SelectedItemKey = target;
            var anchor = tree[list.FirstVisibleIndex].Key;
            tree.SetExpanded("root:0", false);
            await list.PendingChange;
            Debug.Log($"MUI Tree reveal: status={reveal.Status}; visible={tree.Count}; " +
                $"anchorKept={Equals(anchor, tree[list.FirstVisibleIndex].Key)}");

            tree.SetExpanded("root:7", false);
            await list.PendingChange;
            Debug.Log($"MUI Tree collapsed: targetIndex={tree.GetVisibleIndex(target)}; " +
                $"selectionCleared={list.SelectedKey == null}; descendantStateKept={tree.IsExpanded("branch:7:2")}");
            tree.ExpandAncestors(target);
            await list.PendingChange;
            nodes.Reverse();
            tree.Reset(nodes);
            await list.PendingChange;
            Debug.Log($"MUI Tree reordered: firstKey={tree[0].Key}; " +
                $"root0Collapsed={!tree.IsExpanded("root:0")}; targetVisible={tree.GetVisibleIndex(target) >= 0}");

            tree.SetAllExpanded(false);
            await list.PendingChange;
            Debug.Log($"MUI Tree roots only: visible={tree.Count}; total={tree.TotalNodeCount}; cells={list.MaterializedCount}");
            tree.ExpandAncestors(target);
            await list.PendingChange;
            Debug.Log($"MUI Tree restore path: visible={tree.Count}; reveal={(await list.ScrollToKeyAsync(target)).Status}");
        }

        private static void AddTreeNode(List<TreeNode<VirtualListItem>> nodes,
            string key, string parentKey, int depth, bool expanded = false)
        {
            // 缩进和展开图标属于节点表现；本例只用文本缩进，数据层不创建 Transform 层级。
            var model = new ThingItemViewModel { Label = new string(' ', depth * 2) + key };
            var row = new VirtualListItem(key, model, height: depth == 0 ? 50 : 40,
                templateKey: depth == 0 ? "featured" : null);
            nodes.Add(new TreeNode<VirtualListItem>(row, parentKey, expanded));
        }
    }
}
