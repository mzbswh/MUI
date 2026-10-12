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

        public int DefinitionCount
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

        public string ExportText() => ExportText(L.Get("editor.UIBuildValidationReport.title"));

        internal string ExportText(string title)
        {
            var text = new StringBuilder();
            var status = CatalogCount == 0 ? L.Get("editor.UIBuildValidationReport.unverified") :
                IsValid ? L.Get("editor.UIBuildValidationReport.passed") :
                L.Format("editor.UIBuildValidationReport.failed", issues.Count + (IsTruncated ? "+" : ""));
            text.Append(title).Append("：").AppendLine(status);
            text.AppendLine(L.Format("editor.UIBuildValidationReport.counts", CatalogCount, PageCount, DefinitionCount));
            if (CatalogCount == 0)
            {
                text.AppendLine(L.Get("editor.UIBuildValidationReport.0dbf27f994"));
            }
            for (var index = 0; index < issues.Count; index++)
            {
                text.Append(index + 1).Append(". ").AppendLine(issues[index]);
            }
            if (IsTruncated)
            {
                text.AppendLine(L.Get("editor.UIBuildValidationReport.60b24dd7ab"));
            }
            return text.ToString();
        }
    }
}
