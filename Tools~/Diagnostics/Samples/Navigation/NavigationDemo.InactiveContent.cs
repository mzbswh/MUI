using System;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        [ContextMenu("Clear Inactive UI Content And Log")]
        private void TrimInactiveMemory()
        {
            if (host != null)
            {
                _ = TrimInactiveMemoryAsync();
            }
        }

        private async Task TrimInactiveMemoryAsync()
        {
            try
            {
                var pages = host.Pages;
                var historyCount = pages.History.Count;
                await host.ClearInactiveContentAsync();
                Debug.Log($"MUI 闲置界面清理：历史={historyCount} -> {pages.History.Count}，" +
                    $"缓存={pages.CachedViewCount}，预加载={pages.PreloadReservationCount}");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
