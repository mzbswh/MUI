using System.Collections.Generic;
using System.Text;
using L = MUI.Editor.Localization.MUIEditorLocalization;

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
            if (message.Length > 2048)
            {
                var length = char.IsHighSurrogate(message[2046]) ? 2046 : 2047;
                message = message.Substring(0, length) + "…";
            }
            issues.Add(message);
        }

        public string ExportText()
        {
            var text = new StringBuilder();
            text.Append(L.Get("editor.UIBuildValidationReport.b79876b776")).Append(CatalogCount).Append(L.Get("editor.UIBuildValidationReport.0fb3e9344a")).Append(PageCount)
                .Append(L.Get("editor.UIBuildValidationReport.c636b7b038")).Append(RouteCount).AppendLine();
            if (CatalogCount == 0)
            {
                text.AppendLine(L.Get("editor.UIBuildValidationReport.0dbf27f994"));
            }
            foreach (var issue in issues)
            {
                text.AppendLine(issue);
            }
            if (IsTruncated)
            {
                text.AppendLine(L.Get("editor.UIBuildValidationReport.60b24dd7ab"));
            }
            return text.ToString();
        }
    }
}
