using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>同一个虚拟列表混排组标题与正文，演示折叠、锚点、选择和分组重排。</summary>
        private static async Task DemonstrateGroupedListAsync(PageViewModel model, VirtualListElement list)
        {
            var definitions = new List<ListGroup<VirtualListItem>>();
            for (var groupIndex = 0; groupIndex < 100; ++groupIndex)
            {
                var body = new List<VirtualListItem>();
                for (var itemIndex = 0; itemIndex < 25; ++itemIndex)
                {
                    var key = groupIndex * 25 + itemIndex;
                    body.Add(new VirtualListItem(key,
                        new ThingItemViewModel { Label = "Group " + groupIndex + " / Item " + itemIndex }));
                }

                // 标题借用现有 featured 模板，正文使用默认模板，两者仍是普通子视图。
                var header = new VirtualListItem("group:" + groupIndex,
                    new ThingItemViewModel { Label = "Group " + groupIndex }, height: 50, templateKey: "featured");
                definitions.Add(new ListGroup<VirtualListItem>(groupIndex, header, body));
            }

            var grouped = new GroupedList<VirtualListItem>(item => item.Key, maxItems: 3000, maxGroups: 100);
            grouped.Reset(definitions);
            model.VirtualItems = grouped;
            await list.PendingChange;
            Debug.Log($"MUI Grouped list: groups={grouped.GroupCount}; total={grouped.TotalItemCount}; " +
                $"visible={grouped.Count}; cells={list.MaterializedCount}");

            await list.ScrollToKeyAsync(251);
            model.SelectedItemKey = 251;
            var anchor = grouped[list.FirstVisibleIndex].Key;
            grouped.SetExpanded(0, false);
            await list.PendingChange;
            Debug.Log($"MUI Grouped collapse before anchor: visible={grouped.Count}; " +
                $"anchorKept={Equals(anchor, grouped[list.FirstVisibleIndex].Key)}; selected={list.SelectedKey}");

            grouped.SetExpanded(10, false);
            await list.PendingChange;
            var hiddenReveal = await list.ScrollToKeyAsync(251);
            Debug.Log($"MUI Grouped collapse selected: visible={grouped.Count}; " +
                $"selectionCleared={list.SelectedKey == null}; hiddenReveal={hiddenReveal.Status}");

            definitions.Reverse();
            grouped.Reset(definitions);
            await list.PendingChange;
            Debug.Log($"MUI Grouped reorder: firstGroup={grouped.GetGroup(0).Key}; " +
                $"collapseKept={!grouped.IsExpanded(0) && !grouped.IsExpanded(10)}");

            grouped.SetExpanded(10, true);
            await list.PendingChange;
            var restored = await list.ScrollToKeyAsync(251, focus: true);
            Debug.Log($"MUI Grouped expand and reveal: status={restored.Status}; visible={grouped.Count}");
            grouped.SetAllExpanded(false);
            await list.PendingChange;
            Debug.Log($"MUI Grouped headers only: total={grouped.TotalItemCount}; " +
                $"visible={grouped.Count}; cells={list.MaterializedCount}");
        }
    }
}
