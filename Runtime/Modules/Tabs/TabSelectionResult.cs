using System;

namespace MUI.Tabs
{
    /// <summary>一次选择或等待的结果；WaitCancelled 仅取消等待，Superseded 表示被新意图取代。</summary>
    public enum TabSelectionStatus
    {
        Ready,
        Superseded,
        /// <summary>目标选择被取消；已保留旧页可恢复，不能据此推断内容为空。</summary>
        Cancelled,
        Failed,
        ParentInactive,
        Rejected,
        WaitCancelled,
        Empty
    }

    /// <summary>请求在开始切换前被拒绝的原因。</summary>
    public enum TabRejection
    {
        None,
        UnknownTab,
        Disabled,
        Reentrant,
        Denied
    }

    /// <summary>选择结果，不持有子界面或资源所有权。</summary>
    public readonly struct TabSelectionResult
    {
        internal TabSelectionResult(TabSelectionStatus status, TabRejection rejection = TabRejection.None, Exception error = null)
        {
            Status = status;
            Rejection = rejection;
            Error = error;
        }

        public TabSelectionStatus Status
        {
            get;
        }

        public TabRejection Rejection
        {
            get;
        }

        public Exception Error
        {
            get;
        }
    }
}
