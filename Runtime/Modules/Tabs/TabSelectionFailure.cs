using System;

namespace MUI.Tabs
{
    /// <summary>一次切换失败通知；恢复旧内容后仍能独立报告原目标失败，不持有界面或资源。</summary>
    public sealed class TabSelectionFailure
    {
        internal TabSelectionFailure(string failedTab, string restoredTab, long requestVersion, Exception error)
        {
            FailedTab = failedTab;
            RestoredTab = restoredTab;
            RequestVersion = requestVersion;
            Error = error;
        }

        public string FailedTab
        {
            get;
        }

        /// <summary>成功恢复的旧键；未恢复时为 null。</summary>
        public string RestoredTab
        {
            get;
        }

        public long RequestVersion
        {
            get;
        }

        public Exception Error
        {
            get;
        }
    }
}
