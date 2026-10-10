using System;
using System.Collections.Generic;
using System.Text;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Navigation.Editor
{
    /// <summary>编辑器保存纯元数据快照，不跨播放会话持有路由工厂及其捕获的项目对象。</summary>
    [Serializable]
    internal sealed class RouteGraphSnapshot
    {
        internal const int MaxNodes = 2048;
        internal const int MaxEdges = 32768;
        public string RootKey;
        public string CapturedAt;
        public int TotalNodes;
        public bool Truncated;
        public bool Valid;
        public List<Node> Nodes = new List<Node>();
        public List<Edge> Edges = new List<Edge>();
        public List<Issue> Issues = new List<Issue>();

        internal static RouteGraphSnapshot Capture(Route root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            var validation = RouteGraphValidator.Validate(root, maxIssues: 256);
            var result = new RouteGraphSnapshot
            {
                RootKey = root.Key,
                CapturedAt = DateTime.UtcNow.ToString("u"),
                TotalNodes = validation.RouteCount,
                Valid = validation.IsValid,
                Truncated = validation.IsTruncated || validation.RouteCount > MaxNodes
            };
            var indices = new Dictionary<Route, int>();
            var count = Math.Min(validation.Routes.Count, MaxNodes);
            for (var i = 0; i < count; ++i)
            {
                var route = validation.Routes[i];
                indices.Add(route, i);
                result.Nodes.Add(new Node
                {
                    Key = route.Key,
                    Resource = route.Resource.ToString(),
                    Layer = route.Policy.Layer,
                    AllowMultiple = route.Policy.AllowMultiple,
                    MaxInstances = route.Policy.MaxInstances,
                    TakesFocus = route.Policy.TakesFocus,
                    CacheMode = route.Policy.CacheMode
                });
            }
            for (var i = 0; i < count; ++i)
            {
                foreach (var descriptor in validation.Routes[i].DependencyDescriptors)
                {
                    if (result.Edges.Count >= MaxEdges)
                    {
                        result.Truncated = true;
                        break;
                    }
                    var target = descriptor.Target;
                    result.Edges.Add(new Edge
                    {
                        Owner = i,
                        Target = target != null && indices.TryGetValue(target, out var index) ? index : -1,
                        TargetKey = target == null ? L.Get("editor.RouteGraphSnapshot.51cb866ffa") : target.Key,
                        Required = descriptor.IsRequired,
                        Placement = descriptor.Placement.ToString(),
                        MissingPolicy = descriptor.MissingPolicy
                    });
                }
            }
            foreach (var issue in validation.Issues)
            {
                result.Issues.Add(new Issue { Key = issue.RouteKey, Code = issue.Rejection, Message = issue.Message });
            }
            return result;
        }

        internal string CreateReport()
        {
            var report = new StringBuilder();
            report.AppendLine(L.Format("editor.RouteGraphSnapshot.9cc9fa8b20", RootKey));
            report.AppendLine(L.Format("editor.RouteGraphSnapshot.158410a533", CapturedAt, Valid, TotalNodes, Truncated));
            report.AppendLine(L.Get("editor.RouteGraphSnapshot.b375d3caee"));
            for (var i = 0; i < Nodes.Count; ++i)
            {
                var node = Nodes[i];
                report.AppendLine(L.Format("editor.RouteGraphSnapshot.9d4ce01625", i + 1, node.Key, node.Layer, node.Resource));
                report.AppendLine("  " + node.DescribePolicy());
            }
            foreach (var edge in Edges)
            {
                report.AppendLine($"#{edge.Owner + 1} → {(edge.Target < 0 ? L.Get("editor.RouteGraphSnapshot.01c47b74d0") : "#" + (edge.Target + 1))} {edge.TargetKey} | {(edge.Required ? L.Get("editor.RouteGraphSnapshot.92707d3318") : L.Get("editor.RouteGraphSnapshot.537352400c"))} | {L.EnumName(typeof(DependencyPlacement), edge.Placement)} | {L.Value(edge.MissingPolicy)}");
            }
            foreach (var issue in Issues)
            {
                report.AppendLine(L.Format("editor.RouteGraphSnapshot.c4d58b520a", issue.Key, issue.Code, L.Diagnostic(issue.Message)));
            }
            return report.ToString();
        }

        [Serializable]
        internal sealed class Node
        {
            public string Key;
            public string Resource;
            public int Layer;
            public bool AllowMultiple;
            public int MaxInstances;
            public bool TakesFocus;
            public ViewCacheMode CacheMode;

            internal string DescribePolicy() => L.Format("editor.RouteGraphSnapshot.726b6750c3", AllowMultiple, MaxInstances, TakesFocus, CacheMode);
        }

        [Serializable]
        internal sealed class Edge
        {
            public int Owner;
            public int Target;
            public string TargetKey;
            public bool Required;
            public string Placement;
            public DependencyMissingPolicy MissingPolicy;
        }

        [Serializable]
        internal sealed class Issue
        {
            public string Key;
            public OpenRejection Code;
            public string Message;
        }
    }
}
