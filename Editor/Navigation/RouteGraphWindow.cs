using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MUI.Navigation.Editor
{
    /// <summary>静态依赖图浏览器。只保存可序列化诊断快照，不保存或调用项目工厂。</summary>
    public sealed partial class RouteGraphWindow : EditorWindow
    {
        [SerializeField] private List<RouteGraphSnapshot> graphs = new List<RouteGraphSnapshot>();
        [SerializeField] private int selectedGraph;
        [SerializeField] private int selectedNode;
        private readonly List<int> filteredNodes = new List<int>();
        private DropdownField roots;
        private ToolbarSearchField search;
        private ListView nodes;
        private ScrollView details;
        private Label summary;
        private Label empty;
        private VisualElement split;
        private Button copyButton;
        private Button rootButton;
        private bool updating;

        private RouteGraphSnapshot Current => graphs.Count == 0 ? null : graphs[selectedGraph];

        [MenuItem("Tools/MUI/路由依赖图")]
        public static void Open() => GetWindow<RouteGraphWindow>("MUI 路由依赖图");

        /// <summary>在编辑器主线程显式采集并展示一个根；不执行参数、模型、绑定或资源工厂。</summary>
        public static void Show(Route root) => Show(new[] { root });

        /// <summary>各根独立校验；最多 32 个根，避免一次窗口采集无界项目目录。</summary>
        public static void Show(IReadOnlyList<Route> roots)
        {
            if (roots == null || roots.Count == 0 || roots.Count > 32)
            {
                throw new ArgumentException("请提供 1 至 32 个路由根。", nameof(roots));
            }
            // 先完整采集，再替换窗口内容。异常不能把原诊断快照覆盖成半份结果。
            var snapshots = new List<RouteGraphSnapshot>(roots.Count);
            foreach (var route in roots)
            {
                snapshots.Add(RouteGraphSnapshot.Capture(route));
            }
            var window = GetWindow<RouteGraphWindow>("MUI 路由依赖图");
            window.graphs = snapshots;
            window.selectedGraph = 0;
            window.selectedNode = 0;
            window.RenderGraph();
            window.Focus();
        }

        public void CreateGUI()
        {
            minSize = new Vector2(700, 420);
            rootVisualElement.Clear();
            var script = MonoScript.FromScriptableObject(this);
            var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(script));
            var path = (directory ?? string.Empty).Replace('\\', '/') + "/RouteGraphWindow.uxml";
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (template == null)
            {
                rootVisualElement.Add(new HelpBox("依赖图窗口缺少模板：" + path, HelpBoxMessageType.Error));
                return;
            }
            template.CloneTree(rootVisualElement);
            roots = rootVisualElement.Q<DropdownField>("roots");
            search = rootVisualElement.Q<ToolbarSearchField>("search");
            nodes = rootVisualElement.Q<ListView>("nodes");
            details = rootVisualElement.Q<ScrollView>("details");
            summary = rootVisualElement.Q<Label>("summary");
            empty = rootVisualElement.Q<Label>("empty");
            split = rootVisualElement.Q<VisualElement>("split");
            copyButton = rootVisualElement.Q<Button>("copyButton");
            rootButton = rootVisualElement.Q<Button>("rootButton");
            nodes.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("route-graph-node");
                return label;
            };
            nodes.bindItem = BindNode;
            nodes.selectionChanged += OnNodeSelection;
            roots.RegisterValueChangedCallback(_ =>
            {
                if (!updating)
                {
                    selectedGraph = roots.index;
                    selectedNode = 0;
                    RenderGraph();
                }
            });
            search.RegisterValueChangedCallback(_ => FilterNodes());
            copyButton.clicked += () => EditorGUIUtility.systemCopyBuffer = Current.CreateReport();
            rootButton.clicked += () => NavigateTo(0);
            rootVisualElement.Q<Button>("clearButton").clicked += () =>
            {
                graphs.Clear();
                selectedGraph = 0;
                selectedNode = 0;
                RenderGraph();
            };
            RenderGraph();
        }

        private void RenderGraph()
        {
            if (roots == null)
            {
                return;
            }
            selectedGraph = graphs.Count == 0 ? 0 : Math.Max(0, Math.Min(selectedGraph, graphs.Count - 1));
            updating = true;
            var choices = new List<string>();
            for (var i = 0; i < graphs.Count; ++i)
            {
                choices.Add($"{i + 1}. {graphs[i].RootKey}");
            }
            roots.choices = choices;
            roots.SetValueWithoutNotify(choices.Count == 0 ? string.Empty : choices[selectedGraph]);
            roots.SetEnabled(choices.Count != 0);
            copyButton.SetEnabled(choices.Count != 0);
            rootButton.SetEnabled(choices.Count != 0);
            empty.EnableInClassList("route-graph-hidden", choices.Count != 0);
            split.EnableInClassList("route-graph-hidden", choices.Count == 0);
            search.SetValueWithoutNotify(string.Empty);
            var graph = Current;
            summary.text = graph == null ? "未采集路由元数据。" :
                $"{(graph.Valid ? "静态校验通过" : "存在静态错误")} · 节点 {graph.Nodes.Count}/{graph.TotalNodes} · 关系 {graph.Edges.Count} · 问题 {graph.Issues.Count} · {graph.CapturedAt}" +
                (graph.Truncated ? " · 快照或问题列表已截断，不代表完整图" : string.Empty);
            updating = false;
            FilterNodes();
            ShowNode(selectedNode);
        }

        private void BindNode(VisualElement element, int index)
        {
            var graph = Current;
            var id = filteredNodes[index];
            var node = graph.Nodes[id];
            var label = (Label)element;
            label.text = $"#{id + 1} {node.Key}\nLayer {node.Layer} · {(node.Synchronous ? "支持同步" : "异步生命周期")}";
            label.tooltip = node.Resource;
            label.EnableInClassList("is-error", graph.Issues.Exists(issue => issue.Key == node.Key));
        }

        private void FilterNodes()
        {
            if (nodes == null)
            {
                return;
            }
            filteredNodes.Clear();
            var graph = Current;
            if (graph != null)
            {
                var query = search.value ?? string.Empty;
                for (var i = 0; i < graph.Nodes.Count; ++i)
                {
                    var node = graph.Nodes[i];
                    if (node.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        node.Resource.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filteredNodes.Add(i);
                    }
                }
            }
            var wasUpdating = updating;
            updating = true;
            try
            {
                // 切换图或过滤时，列表重建产生的旧选择通知不能覆盖新的选中节点。
                nodes.itemsSource = filteredNodes;
                nodes.Rebuild();
                var selectedIndex = filteredNodes.IndexOf(selectedNode);
                if (selectedIndex < 0)
                {
                    nodes.ClearSelection();
                }
                else
                {
                    nodes.SetSelectionWithoutNotify(new[] { selectedIndex });
                }
            }
            finally
            {
                updating = wasUpdating;
            }
        }

        private void OnNodeSelection(IEnumerable<object> selection)
        {
            if (updating)
            {
                return;
            }
            foreach (var value in selection)
            {
                ShowNode((int)value);
                return;
            }
        }

        private void NavigateTo(int id)
        {
            search.SetValueWithoutNotify(string.Empty);
            selectedNode = id;
            FilterNodes();
            ShowNode(id);
            var index = filteredNodes.IndexOf(id);
            if (index >= 0)
            {
                nodes.ScrollToItem(index);
            }
        }
    }
}
