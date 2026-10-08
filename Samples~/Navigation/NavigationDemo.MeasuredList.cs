using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateMeasuredListAsync(View view, PageViewModel model)
        {
            var list = view.GetElement<VirtualListElement>("VirtualItems");
            var values = new List<VirtualListItem>();
            for (var index = 0; index < 2000; ++index)
            {
                var text = "Message " + index;
                if (index % 3 != 0)
                {
                    text += "\nA longer message wraps to several lines when the viewport becomes narrow.";
                }

                if (index % 3 == 2)
                {
                    text += "\nThe list measures visible rows and keeps the current reading position.";
                }

                values.Add(new VirtualListItem(index, new ThingItemViewModel { Label = text },
                    templateKey: index % 5 == 0 ? "featured" : null));
            }

            var source = new ObservableList<VirtualListItem>(values, itemKey: item => item.Key);
            model.VirtualItems = source;
            var initial = await list.ScrollToKeyAsync(1500, focus: true, cancellationToken: cancellation.Token);
            Debug.Log($"MUI Measured list: initial={initial.Status}; first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
            if (initial.Status != VirtualListRevealStatus.Ready || view == null || !view.IsAlive)
            {
                return;
            }

            var scroll = list.GetComponentInChildren<ScrollRect>();
            scroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 140);
            var narrowed = await list.ScrollToKeyAsync(1500, cancellationToken: cancellation.Token);
            Debug.Log($"MUI Measured list narrow: result={narrowed.Status}; first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
            if (narrowed.Status != VirtualListRevealStatus.Ready || view == null || !view.IsAlive)
            {
                return;
            }

            // 字体变化影响模板及当前实例，显式失效缓存；屏外实例将在创建后采用新模板字号。
            foreach (var text in list.GetComponentsInChildren<Text>(true))
            {
                if (text.gameObject.name == "ItemLabel")
                {
                    text.fontSize = 24;
                }
            }

            list.InvalidateSizeMeasurements();
            var resized = await list.ScrollToKeyAsync(1500, cancellationToken: cancellation.Token);
            Debug.Log($"MUI Measured list font: result={resized.Status}; first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
            if (resized.Status != VirtualListRevealStatus.Ready || view == null || !view.IsAlive)
            {
                return;
            }

            ((ThingItemViewModel)source[1500].ViewModel).Label += "\nContent changed after the first measurement.";
            source.NotifyUpdated(1500);
            var updated = await list.ScrollToKeyAsync(1500, cancellationToken: cancellation.Token);
            Debug.Log($"MUI Measured list update: result={updated.Status}; first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
        }
    }
}
