using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

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
            AddText(L.Format("editor.RouteGraphWindow.Details.655f0f0b1a", node.Resource));
            AddText(L.Format("editor.RouteGraphWindow.Details.5e80d84deb", node.Layer));
            AddText(node.DescribePolicy());
            AddText(L.Get("editor.RouteGraphWindow.Details.840cc3e250"));
            if (graph.Truncated)
            {
                details.Add(new HelpBox(L.Get("editor.RouteGraphWindow.Details.936ac0c9d9"), HelpBoxMessageType.Warning));
            }
            ShowRelations(id, incoming: true);
            ShowRelations(id, incoming: false);
            AddText(L.Get("editor.RouteGraphWindow.Details.76f5a111c9"), "route-graph-heading");
            if (graph.Issues.Count == 0)
            {
                AddText(L.Get("editor.RouteGraphWindow.Details.efbcfd92f1"));
            }
            foreach (var issue in graph.Issues)
            {
                var target = graph.Nodes.FindIndex(candidate => candidate.Key == issue.Key);
                var destination = target;
                var button = new Button(() => NavigateTo(destination))
                {
                    text = $"{issue.Key} · {L.Value(issue.Code)}\n{L.Diagnostic(issue.Message)}"
                };
                button.AddToClassList("route-graph-edge");
                button.SetEnabled(target >= 0);
                details.Add(button);
            }
        }

        private void ShowRelations(int id, bool incoming)
        {
            var graph = Current;
            AddText(incoming ? L.Get("editor.RouteGraphWindow.Details.2d40633e66") : L.Get("editor.RouteGraphWindow.Details.695a39529f"), "route-graph-heading");
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
                var placement = edge.Placement == nameof(DependencyPlacement.RequiredBefore) ? L.Get("editor.RouteGraphWindow.Details.a4c1e252ea") :
                    edge.Placement == nameof(DependencyPlacement.AttachedAfter) ? L.Get("editor.RouteGraphWindow.Details.7cbe752ca7") : L.Get("editor.RouteGraphWindow.Details.273b96485f") + edge.Placement;
                var layerNote = edge.Target >= 0 && graph.Nodes[edge.Owner].Layer != graph.Nodes[edge.Target].Layer
                    ? L.Get("editor.RouteGraphWindow.Details.40e06e6a94") : L.Get("editor.RouteGraphWindow.Details.d3fc914f82");
                var title = $"{(incoming ? "←" : "→")} {(target < 0 ? L.Get("editor.RouteGraphWindow.Details.01c47b74d0") : "#" + (target + 1))} {targetKey}";
                var button = new Button(() => NavigateTo(target))
                {
                    text = $"{title}\n{(edge.Required ? L.Get("editor.RouteGraphWindow.Details.5853629b17") : L.Get("editor.RouteGraphWindow.Details.d5adb4d085"))} · {placement} · {L.Value(edge.MissingPolicy)} · {layerNote}"
                };
                button.AddToClassList("route-graph-edge");
                button.SetEnabled(target >= 0);
                details.Add(button);
            }
            if (count == 0)
            {
                AddText(incoming ? L.Get("editor.RouteGraphWindow.Details.404b2b7b9c") : L.Get("editor.RouteGraphWindow.Details.4804324f1f"));
            }
            else if (count > MaxVisibleRelations)
            {
                AddText(L.Format("editor.RouteGraphWindow.Details.9b73c23f45", count, MaxVisibleRelations));
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
