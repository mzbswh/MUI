using System.Collections.Generic;

namespace MUI.Navigation
{
    /// <summary>单项静态声明错误，不包含运行时实例、模型或参数。</summary>
    public readonly struct RouteGraphIssue
    {
        internal RouteGraphIssue(OpenRejection rejection, string routeKey, string message)
        {
            Rejection = rejection;
            RouteKey = routeKey;
            Message = message;
        }

        public OpenRejection Rejection
        {
            get;
        }

        public string RouteKey
        {
            get;
        }

        public string Message
        {
            get;
        }
    }

    /// <summary>一次路由图校验的只读结果；问题数量有上限，未运行任何业务工厂。</summary>
    public sealed class RouteGraphValidationResult
    {
        internal RouteGraphValidationResult(List<Route> routes, List<RouteGraphIssue> issues, bool truncated)
        {
            Routes = routes.AsReadOnly();
            Issues = issues.AsReadOnly();
            IsTruncated = truncated;
        }

        /// <summary>按遍历顺序收录的只读路由集合，首项为校验根；读取不会执行项目工厂。</summary>
        public IReadOnlyList<Route> Routes
        {
            get;
        }

        public IReadOnlyList<RouteGraphIssue> Issues
        {
            get;
        }

        public bool IsValid => Issues.Count == 0;

        public bool IsTruncated
        {
            get;
        }

        public int RouteCount => Routes.Count;
    }
}
