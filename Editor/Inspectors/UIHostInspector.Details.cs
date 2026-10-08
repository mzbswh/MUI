using MUI.Navigation;
using UnityEngine.UIElements;

namespace MUI.Editor
{
    public sealed partial class UIHostInspector
    {
        private void ShowOverview()
        {
            details.Clear();
            if (snapshot == null)
            {
                return;
            }
            AddDetail("快照时间", capturedAt);
            AddHandle("焦点", snapshot.Focused);
            AddDetail("历史", "从旧到新；详情最多展示快照中最近的 128 条。");
            var start = System.Math.Max(0, snapshot.RecentHistory.Count - 128);
            for (var i = start; i < snapshot.RecentHistory.Count; ++i)
            {
                AddHandle("历史", snapshot.RecentHistory[i]);
            }
            if (start > 0 || snapshot.HistoryTruncated)
            {
                details.Add(new HelpBox("历史展示已截断。", HelpBoxMessageType.Warning));
            }
            AddDetail("宿主", snapshot.HostId.ToString("N"));
            AddDetail("清理责任", $"{snapshot.CleanupResponsibilities.Count}/{snapshot.UnconfirmedCleanupCount}；仅包含当前在途或失败责任。");
            foreach (var responsibility in snapshot.CleanupResponsibilities)
            {
                AddDetail("责任", $"{responsibility.Id:N}；{responsibility.Owner}；{responsibility.State}；尝试 {responsibility.Attempts} 次；允许重试 {responsibility.CanRetry}");
                AddDetail("位置", responsibility.Context.ToString());
                if (responsibility.Failure != null)
                {
                    AddDetail("错误", $"{responsibility.DiagnosticId:N}；{responsibility.Failure.Message}");
                }
            }
            if (snapshot.CleanupResponsibilitiesTruncated)
            {
                details.Add(new HelpBox("清理责任已截断；实际责任仍由全局账本持有。", HelpBoxMessageType.Warning));
            }
        }

        private void ShowInstance(ViewInstanceSnapshot item)
        {
            details.Clear();
            AddDetail("快照时间", capturedAt);
            AddDetail("路由", item.RouteKey);
            AddDetail("完整句柄", item.Handle.ToString());
            AddDetail("资源 / 版本", item.ResourceKey + " / " + item.ResourceVersion);
            AddDetail("状态 / 清理", item.State + " / " + item.Cleanup);
            AddDetail("层 / 置前序号 / 提交版本", $"{item.Layer} / {item.Order} / {item.CommitVersion}");
            AddDetail("显示索引（底到顶）/ 历史索引", $"{item.PresentationIndex} / {item.HistoryIndex}（-1 表示不在集合中）");
            AddDetail("宿主可见 / 允许输入 / 焦点", $"{item.HostVisible} / {item.HostInteractable} / {item.Focused}");
            AddHandle("隐藏来源", item.HiddenBy);
            AddHandle("输入覆盖来源", item.BlockedBy);
            AddDetail("准备完成 / 激活提交", $"{item.PreparationComplete} / {item.ActivationCommitted}");
            AddDetail("正在执行的操作", item.Operations.ToString());
            AddDetail("在途命令数量", item.ExecutingCommandCount.ToString());
            AddDetail("首次就绪", item.HasReadiness ? $"{item.ReadinessStatus}；降级：{item.IsReadinessDegraded}" : "尚未发布");
            AddDetail("实例失败 / 依赖降级数", $"{item.HasFailure} / {item.DependencyFailureCount}");
            AddDetail("显式拥有者 / 父拥有者数", $"{item.HasExplicitOwner} / {item.OwnerCount}");
            foreach (var handle in item.Owners)
            {
                AddHandle("父拥有者", handle);
            }
            if (item.OwnersTruncated)
            {
                details.Add(new HelpBox("父拥有者只显示快照保留的前 128 条。", HelpBoxMessageType.Warning));
            }
            AddDetail("直接依赖数", item.Dependencies.Count.ToString());
            foreach (var handle in item.Dependencies)
            {
                AddHandle("直接依赖", handle);
            }
        }

        private void AddDetail(string label, string value)
        {
            var element = new Label(label + "：" + value);
            element.AddToClassList("mui-host-detail");
            details.Add(element);
        }

        private void AddHandle(string label, ViewHandle handle)
        {
            if (!handle.IsValid)
            {
                AddDetail(label, "无");
                return;
            }
            var button = new Button(() => NavigateTo(handle))
            {
                text = $"{label}：#{handle.Id}（查看快照）",
                tooltip = handle.ToString()
            };
            details.Add(button);
        }
    }
}
