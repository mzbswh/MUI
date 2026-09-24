using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>用受控迟到页演示快速切换查询，只应用最后一次请求且不回写旧数据。</summary>
        private async Task DemonstratePagingResetAsync(Lifetime owner, PageViewModel model, VirtualListElement list)
        {
            var releaseOldPage = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var middleLoads = 0;
            var latestLoads = 0;
            var source = new PagedList<VirtualListItem, int>(owner, async (cursor, size, token) =>
            {
                // 故意不响应取消，模拟已经发出、只能等待返回的项目请求。
                await releaseOldPage.Task;
                Debug.Log($"MUI Paging reset old request: cancellationRequested={token.IsCancellationRequested}");
                return new ListPage<VirtualListItem, int>(new[]
                {
                    new VirtualListItem(0, new ThingItemViewModel { Label = "Old query" })
                });
            }, item => item.Key, pageSize: 3, maxItems: 9);

            try
            {
                model.VirtualItems = source;
                var oldPage = list.LoadNextPageAsync().AsTask();
                var firstReset = source.ResetAsync(loader: (cursor, size, token) =>
                {
                    ++middleLoads;
                    return new ValueTask<ListPage<VirtualListItem, int>>(
                        new ListPage<VirtualListItem, int>(new VirtualListItem[0]));
                }).AsTask();
                var latestReset = source.ResetAsync(loader: (cursor, size, token) =>
                {
                    ++latestLoads;
                    var page = new List<VirtualListItem>();
                    for (var index = 0; index < size; ++index)
                    {
                        page.Add(new VirtualListItem(100 + index,
                            new ThingItemViewModel { Label = "New query " + index }));
                    }

                    return new ValueTask<ListPage<VirtualListItem, int>>(
                        new ListPage<VirtualListItem, int>(page));
                }).AsTask();
                var duringReset = await source.LoadNextAsync();
                releaseOldPage.TrySetResult(true);
                var oldResult = await oldPage;
                var superseded = await firstReset;
                var applied = await latestReset;
                var newPage = await list.LoadNextPageAsync();
                await list.PendingChange;
                Debug.Log($"MUI Paging reset: old={oldResult.Status}; first={superseded.Status}; " +
                    $"latest={applied.Status}; during={duringReset.Status}; new={newPage.Status}; " +
                    $"middleLoads={middleLoads}; latestLoads={latestLoads}; count={source.Count}; " +
                    $"firstKey={(source.Count == 0 ? null : source[0].Key)}");
            }
            finally
            {
                // 演示自身失败或关闭也要放行迟到任务，不能阻塞父生命周期排空。
                releaseOldPage.TrySetResult(true);
            }
        }
    }
}
