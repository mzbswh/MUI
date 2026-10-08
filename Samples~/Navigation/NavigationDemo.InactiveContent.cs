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
                var navigator = host.Navigator;
                var historyCount = navigator.History.Count;
                await host.ClearInactiveContentAsync();
                Debug.Log($"MUI 闲置界面清理：历史={historyCount} -> {navigator.History.Count}，" +
                    $"缓存={navigator.CachedViewCount}，预加载={navigator.PreloadReservationCount}");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
