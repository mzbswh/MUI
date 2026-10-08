using System;
using System.Collections.Generic;
using System.Text;

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
                    Policy = $"多实例={route.Policy.AllowMultiple}，上限={route.Policy.MaxInstances}，历史={route.Policy.EnterHistory}，焦点={route.Policy.TakesFocus}，缓存={route.Policy.CacheMode}"
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
                        TargetKey = target == null ? "无效目标" : target.Key,
                        Required = descriptor.IsRequired,
                        Placement = descriptor.Placement.ToString()
                    });
                }
            }
            foreach (var issue in validation.Issues)
            {
                result.Issues.Add(new Issue { Key = issue.RouteKey, Code = issue.Rejection.ToString(), Message = issue.Message });
            }
            return result;
        }

        internal string CreateReport()
        {
            var report = new StringBuilder();
            report.AppendLine($"MUI 路由依赖图：{RootKey}");
            report.AppendLine($"采集时间：{CapturedAt}；静态有效：{Valid}；总节点：{TotalNodes}；截断：{Truncated}");
            report.AppendLine("此报告不验证运行时参数、资源可用性或跨根共享组合。");
            for (var i = 0; i < Nodes.Count; ++i)
            {
                var node = Nodes[i];
                report.AppendLine($"#{i + 1} {node.Key} | Layer={node.Layer} | {node.Resource}");
                report.AppendLine("  " + node.Policy);
            }
            foreach (var edge in Edges)
            {
                report.AppendLine($"#{edge.Owner + 1} → {(edge.Target < 0 ? "未收录" : "#" + (edge.Target + 1))} {edge.TargetKey} | {(edge.Required ? "必需" : "可选")} | {edge.Placement}");
            }
            foreach (var issue in Issues)
            {
                report.AppendLine($"错误 {issue.Key} / {issue.Code}：{issue.Message}");
            }
            return report.ToString();
        }

        [Serializable]
        internal sealed class Node
        {
            public string Key;
            public string Resource;
            public int Layer;
            public string Policy;
        }

        [Serializable]
        internal sealed class Edge
        {
            public int Owner;
            public int Target;
            public string TargetKey;
            public bool Required;
            public string Placement;
        }

        [Serializable]
        internal sealed class Issue
        {
            public string Key;
            public string Code;
            public string Message;
        }
    }
}
