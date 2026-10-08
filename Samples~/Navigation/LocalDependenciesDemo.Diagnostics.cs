using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class LocalDependenciesDemo
    {
        [ContextMenu("本地打印导航状态快照")]
        public void PrintNavigationSnapshot()
        {
            if (!initialized || host == null)
            {
                return;
            }
            var snapshot = host.Navigator.CaptureSnapshot();
            Debug.Log($"导航快照：实例={snapshot.Instances.Count}/{snapshot.TotalInstances}，表现稳定={snapshot.IsPresentationSettled}，焦点={snapshot.Focused}，历史={snapshot.HistoryCount}，缓存={snapshot.CachedViewCount}，待清理={snapshot.PendingCleanupCount}");
            foreach (var instance in snapshot.Instances)
            {
                Debug.Log($"{instance.RouteKey} [{instance.Handle.Id}]：状态={instance.State}，显示索引={instance.PresentationIndex}，可见={instance.HostVisible}，导航交互={instance.HostInteractable}，隐藏来源={instance.HiddenBy}，输入覆盖来源={instance.BlockedBy}，操作={instance.Operations}，父拥有者={instance.Owners.Count}/{instance.OwnerCount}，显式持有={instance.HasExplicitOwner}，依赖={instance.Dependencies.Count}");
            }
        }
    }
}
