using System;

namespace MUI.UGUI
{
    /// <summary>当前来源中一次条目准备失败；读取或重复观察不重新报告异常。</summary>
    public sealed class VirtualListItemFailure
    {
        internal VirtualListItemFailure(VirtualListItem item, long sourceGeneration, Exception error)
        {
            Item = item;
            SourceGeneration = sourceGeneration;
            Error = error;
        }

        public VirtualListItem Item
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        internal long SourceGeneration
        {
            get;
        }
    }
}
