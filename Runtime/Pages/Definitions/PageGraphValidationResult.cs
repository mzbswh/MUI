using System.Collections.Generic;

namespace MUI.Pages
{
    /// <summary>单项静态声明错误，不包含运行时实例、模型或参数。</summary>
    public readonly struct PageGraphIssue
    {
        internal PageGraphIssue(OpenRejection rejection, string pageKey, string message)
        {
            Rejection = rejection;
            PageKey = pageKey;
            Message = message;
        }

        public OpenRejection Rejection
        {
            get;
        }

        public string PageKey
        {
            get;
        }

        public string Message
        {
            get;
        }
    }

    /// <summary>一次页面定义图校验的只读结果；问题数量有上限，未运行任何业务工厂。</summary>
    public sealed class PageGraphValidationResult
    {
        internal PageGraphValidationResult(List<PageDefinition> routes, List<PageGraphIssue> issues, bool truncated)
        {
            Definitions = routes.AsReadOnly();
            Issues = issues.AsReadOnly();
            IsTruncated = truncated;
        }

        /// <summary>按遍历顺序收录的只读页面定义集合，首项为校验根；读取不会执行项目工厂。</summary>
        public IReadOnlyList<PageDefinition> Definitions
        {
            get;
        }

        public IReadOnlyList<PageGraphIssue> Issues
        {
            get;
        }

        public bool IsValid => Issues.Count == 0;

        public bool IsTruncated
        {
            get;
        }

        public int DefinitionCount => Definitions.Count;
    }
}
