using System;
using System.Threading.Tasks;
using MUI.Navigation;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class AsynchronousDependenciesDemo
    {
        [ContextMenu("打印异步准备与清理快照")]
        public void PrintNavigationSnapshot()
        {
            if (disposed || navigator == null)
            {
                return;
            }
            var snapshot = navigator.CaptureSnapshot();
            Debug.Log($"导航实例={snapshot.Instances.Count}/{snapshot.TotalInstances}，在途请求={snapshot.PendingRequestCount}，待清理={snapshot.PendingCleanupCount}，缓存={snapshot.CachedViewCount}，预加载占位={snapshot.PreloadReservationCount}");
            foreach (var instance in snapshot.Instances)
            {
                Debug.Log($"{instance.RouteKey} [{instance.Handle.Id}]：{instance.State}，操作={instance.Operations}，父拥有者={instance.OwnerCount}，清理={instance.Cleanup}，可选降级数={instance.DependencyFailureCount}");
            }
        }

        [ContextMenu("准备期间强制关闭可选依赖")]
        public void ForceClosePreparingDependency()
        {
            if (disposed || navigator == null || (closingShared != null && !closingShared.IsCompleted))
            {
                return;
            }
            // 只选择本示例的可选目标；不通过修改状态或取消源来伪造关闭。
            foreach (var instance in navigator.CaptureSnapshot().Instances)
            {
                if (instance.RouteKey == "demo.async.failing" &&
                    (instance.Operations & ViewOperationFlags.Preparing) != 0)
                {
                    closingShared = ForceClosePreparingDependencyAsync(instance.Handle);
                    return;
                }
            }
            Debug.Log("尚未进入可选失败依赖的准备阶段，或该阶段已经结束。");
        }

        private async Task ForceClosePreparingDependencyAsync(ViewHandle handle)
        {
            try
            {
                var result = await navigator.ForceCloseAsync(handle);
                Debug.Log($"准备中依赖强制关闭={result.Status}，清理={result.Cleanup}");
                PrintNavigationSnapshot();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
