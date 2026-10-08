using UnityEngine.UIElements;

namespace MUI.Navigation.Editor
{
    public sealed partial class RouteGraphWindow
    {
        private const int MaxVisibleRelations = 128;

        private void ShowNode(int id)
        {
            details.Clear();
            var graph = Current;
            if (graph == null || id < 0 || id >= graph.Nodes.Count)
            {
                return;
            }
            selectedNode = id;
            var node = graph.Nodes[id];
            AddText($"#{id + 1} {node.Key}", "route-graph-heading");
            AddText($"资源：{node.Resource}");
            AddText($"层级：{node.Layer}");
            AddText(node.Policy);
            AddText("父拥有者 → 当前路由 → 直接依赖。点击关系可跳转；同键不同定义以编号区分。");
            if (graph.Truncated)
            {
                details.Add(new HelpBox("当前快照已截断，关系列表可能不完整。复制报告会保留已采集的关系与截断标记。", HelpBoxMessageType.Warning));
            }
            ShowRelations(id, incoming: true);
            ShowRelations(id, incoming: false);
            AddText("本图静态问题", "route-graph-heading");
            if (graph.Issues.Count == 0)
            {
                AddText("未发现静态声明错误；这不证明运行时参数或资源一定可用。");
            }
            foreach (var issue in graph.Issues)
            {
                var target = graph.Nodes.FindIndex(candidate => candidate.Key == issue.Key);
                var destination = target;
                var button = new Button(() => NavigateTo(destination))
                {
                    text = $"{issue.Key} · {issue.Code}\n{issue.Message}"
                };
                button.AddToClassList("route-graph-edge");
                button.SetEnabled(target >= 0);
                details.Add(button);
            }
        }

        private void ShowRelations(int id, bool incoming)
        {
            var graph = Current;
            AddText(incoming ? "父拥有者（指向当前路由）" : "直接依赖（当前路由指向）", "route-graph-heading");
            var count = 0;
            foreach (var edge in graph.Edges)
            {
                if (incoming ? edge.Target != id : edge.Owner != id)
                {
                    continue;
                }
                ++count;
                if (count > MaxVisibleRelations)
                {
                    continue;
                }
                var target = incoming ? edge.Owner : edge.Target;
                var targetKey = incoming ? graph.Nodes[edge.Owner].Key : edge.TargetKey;
                var placement = edge.Placement == nameof(DependencyPlacement.RequiredBefore) ? "父前" :
                    edge.Placement == nameof(DependencyPlacement.AttachedAfter) ? "父后" : "无效顺序：" + edge.Placement;
                var layerNote = edge.Target >= 0 && graph.Nodes[edge.Owner].Layer != graph.Nodes[edge.Target].Layer
                    ? "跨层显示仍按 Layer 排序" : "同层按依赖顺序约束";
                var title = $"{(incoming ? "←" : "→")} {(target < 0 ? "未收录" : "#" + (target + 1))} {targetKey}";
                var button = new Button(() => NavigateTo(target))
                {
                    text = $"{title}\n{(edge.Required ? "必需，失败关闭父链" : "可选，允许缺席降级")} · {placement} · {layerNote}"
                };
                button.AddToClassList("route-graph-edge");
                button.SetEnabled(target >= 0);
                details.Add(button);
            }
            if (count == 0)
            {
                AddText(incoming ? "此校验根的可达图中没有父拥有者。" : "没有直接依赖。");
            }
            else if (count > MaxVisibleRelations)
            {
                AddText($"共 {count} 条关系，此处展示前 {MaxVisibleRelations} 条；其余已采集关系可从复制报告查看。");
            }
        }

        private void AddText(string text, string className = "route-graph-text")
        {
            var label = new Label(text);
            label.AddToClassList(className);
            details.Add(label);
        }
    }
}
