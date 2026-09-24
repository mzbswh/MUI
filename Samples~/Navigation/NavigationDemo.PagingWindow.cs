using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>容量小于两页时持续加载，观察双方向淘汰及阅读锚点回退。</summary>
        private async Task DemonstratePagingWindowAsync(
            Lifetime owner, PageViewModel model, VirtualListElement list, PageInsertion insertion)
        {
            var prepend = insertion == PageInsertion.Prepend;
            var window = new PagedList<VirtualListItem, int>(owner, async (cursor, size, token) =>
            {
                await Task.Delay(30, token);
                var start = prepend ? System.Math.Max(0, cursor - size) : cursor;
                var end = prepend ? cursor : System.Math.Min(100, cursor + size);
                var page = new List<VirtualListItem>();
                for (var index = start; index < end; ++index)
                {
                    page.Add(new VirtualListItem(index,
                        new ThingItemViewModel { Label = "Window " + index }));
                }

                return new ListPage<VirtualListItem, int>(page,
                    prepend ? start : end, prepend ? start > 0 : end < 100);
            }, item => item.Key, initialCursor: prepend ? 100 : 0, pageSize: 20, maxItems: 30,
                insertion: insertion, overflow: PageOverflowPolicy.EvictOppositeEnd);

            model.VirtualItems = window;
            await list.LoadNextPageAsync();
            await list.PendingChange;
            await list.ScrollToKeyAsync(prepend ? 85 : 15);
            for (var pageIndex = 1; pageIndex <= 2; ++pageIndex)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var previousAnchor = window[list.FirstVisibleIndex].Key;
                var result = await list.LoadNextPageAsync();
                await list.PendingChange;
                Debug.Log($"MUI Paging window {insertion}: status={result.Status}; added={result.AddedCount}; " +
                    $"evicted={result.EvictedCount}; count={window.Count}/{window.Capacity}; " +
                    $"keys={window[0].Key}..{window[window.Count - 1].Key}; " +
                    $"anchor={previousAnchor}->{window[list.FirstVisibleIndex].Key}");
            }
        }
    }
}
