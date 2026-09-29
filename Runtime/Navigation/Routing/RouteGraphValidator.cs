using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    /// <summary>读取不可变路由元数据，校验依赖环、同层显示环、深度及键冲突；不加载资源或执行参数工厂。</summary>
    public static class RouteGraphValidator
    {
        public static RouteGraphValidationResult Validate(Route root, int maxIssues = 64)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            if (maxIssues < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIssues));
            }
            var routes = new List<Route>();
            var issues = new List<RouteGraphIssue>();
            var truncated = false;
            void Issue(OpenRejection rejection, Route route, string message)
            {
                if (issues.Count < maxIssues)
                {
                    issues.Add(new RouteGraphIssue(rejection, route.Key, message));
                }
                else
                {
                    truncated = true;
                }
            }

            var visited = new HashSet<Route>();
            var definitions = new Dictionary<string, Route>(StringComparer.Ordinal);
            var dependencies = new Dictionary<Route, HashSet<Route>>();
            var presentation = new Dictionary<Route, HashSet<Route>>();
            var pending = new Queue<Route>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                var route = pending.Dequeue();
                if (!visited.Add(route))
                {
                    continue;
                }
                routes.Add(route);
                if (definitions.TryGetValue(route.Key, out var previous) && !ReferenceEquals(previous, route))
                {
                    Issue(OpenRejection.ConflictingData, route, $"路由键 {route.Key} 对应多个不同定义。");
                }
                else
                {
                    definitions[route.Key] = route;
                }
                EnsureNode(dependencies, route);
                EnsureNode(presentation, route);
                var placements = new Dictionary<Route, DependencyPlacement>();
                var requirements = new Dictionary<Route, bool>();
                if (route.DependencyDescriptors.Count > 64)
                {
                    Issue(OpenRejection.DependencyLimit, route, "路由直接依赖数量超过 64。");
                }
                foreach (var descriptor in route.DependencyDescriptors)
                {
                    var target = descriptor.Target;
                    if (target == null || !Enum.IsDefined(typeof(DependencyPlacement), descriptor.Placement))
                    {
                        Issue(OpenRejection.ConflictingData, route, "依赖元数据缺少目标或使用无效位置。");
                        continue;
                    }
                    if (placements.TryGetValue(target, out var placement))
                    {
                        if (placement != descriptor.Placement)
                        {
                            Issue(OpenRejection.DependencyOrderConflict, route,
                                $"同一父路由对依赖 {target.Key} 声明了不同位置。");
                        }
                        if (requirements[target] != descriptor.IsRequired)
                        {
                            Issue(OpenRejection.ConflictingData, route,
                                $"同一父路由对依赖 {target.Key} 同时声明必需与可选，降级语义不明确。");
                        }
                        if (placement == descriptor.Placement && requirements[target] == descriptor.IsRequired)
                        {
                            Issue(OpenRejection.ConflictingData, route,
                                $"同一父路由重复声明了依赖 {target.Key}。");
                        }

                        continue;
                    }
                    requirements[target] = descriptor.IsRequired;
                    placements[target] = descriptor.Placement;
                    if (target.Policy.AllowMultiple || target.Policy.MaxInstances != 1)
                    {
                        Issue(OpenRejection.ConflictingData, route, $"共享依赖 {target.Key} 不是单实例路由。");
                    }
                    EnsureNode(dependencies, target);
                    EnsureNode(presentation, target);
                    dependencies[route].Add(target);
                    if (route.Policy.Layer == target.Policy.Layer)
                    {
                        var before = descriptor.Placement == DependencyPlacement.RequiredBefore ? target : route;
                        var after = descriptor.Placement == DependencyPlacement.RequiredBefore ? route : target;
                        presentation[before].Add(after);
                    }
                    pending.Enqueue(target);
                }
            }

            var dependencyOrder = TopologicalOrder(dependencies);
            if (dependencyOrder.Count != dependencies.Count)
            {
                Issue(OpenRejection.DependencyCycle, root, "依赖声明形成所有权循环。");
            }
            else
            {
                var depths = new Dictionary<Route, int>();
                foreach (var route in dependencyOrder)
                {
                    depths.TryGetValue(route, out var depth);
                    if (depth > 64)
                    {
                        Issue(OpenRejection.DependencyLimit, route, "从根路由到此依赖的最长路径超过 64 层。");
                    }
                    foreach (var target in dependencies[route])
                    {
                        depths.TryGetValue(target, out var current);
                        if (current < depth + 1)
                        {
                            depths[target] = depth + 1;
                        }
                    }
                }
            }
            if (TopologicalOrder(presentation).Count != presentation.Count)
            {
                Issue(OpenRejection.DependencyOrderConflict, root, "同层依赖显示约束形成循环。");
            }
            return new RouteGraphValidationResult(routes, issues, truncated);
        }

        private static void EnsureNode(Dictionary<Route, HashSet<Route>> graph, Route route)
        {
            if (!graph.ContainsKey(route))
            {
                graph.Add(route, new HashSet<Route>());
            }
        }

        private static List<Route> TopologicalOrder(Dictionary<Route, HashSet<Route>> graph)
        {
            var incoming = new Dictionary<Route, int>();
            foreach (var route in graph.Keys)
            {
                incoming.Add(route, 0);
            }
            foreach (var targets in graph.Values)
            {
                foreach (var target in targets)
                {
                    ++incoming[target];
                }
            }
            var ready = new Queue<Route>();
            foreach (var route in graph.Keys)
            {
                if (incoming[route] == 0)
                {
                    ready.Enqueue(route);
                }
            }
            var result = new List<Route>();
            while (ready.Count > 0)
            {
                var route = ready.Dequeue();
                result.Add(route);
                foreach (var target in graph[route])
                {
                    if (--incoming[target] == 0)
                    {
                        ready.Enqueue(target);
                    }
                }
            }
            return result;
        }
    }
}
