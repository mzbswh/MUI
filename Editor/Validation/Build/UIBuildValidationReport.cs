using System.Collections.Generic;
using System.Text;

namespace MUI.Editor
{
    /// <summary>构建检查元数据；不保留路由工厂、Prefab 或项目异常对象。</summary>
    public sealed class UIBuildValidationReport
    {
        private readonly List<string> issues = new List<string>();

        internal UIBuildValidationReport() => Issues = issues.AsReadOnly();

        public int CatalogCount
        {
            get; internal set;
        }

        public int PageCount
        {
            get; internal set;
        }

        public int RouteCount
        {
            get; internal set;
        }

        public bool IsTruncated
        {
            get; private set;
        }

        public IReadOnlyList<string> Issues
        {
            get;
        }

        public bool IsValid => CatalogCount > 0 && issues.Count == 0 && !IsTruncated;

        internal void Issue(string message)
        {
            if (issues.Count >= 256)
            {
                IsTruncated = true;
                return;
            }
            issues.Add(message.Length > 2048 ? message.Substring(0, 2048) + "…" : message);
        }

        public string ExportText()
        {
            var text = new StringBuilder();
            text.Append("MUI 构建校验：目录 ").Append(CatalogCount).Append("，页面 ").Append(PageCount)
                .Append("，路由 ").Append(RouteCount).AppendLine();
            if (CatalogCount == 0)
            {
                text.AppendLine("未登记 UI 构建目录，本次未验证项目页面覆盖范围。");
            }
            foreach (var issue in issues)
            {
                text.AppendLine(issue);
            }
            if (IsTruncated)
            {
                text.AppendLine("问题超过报告上限，后续明细已省略；本次校验不通过。");
            }
            return text.ToString();
        }
    }
}
