using System;

namespace MUI
{
    public enum RebindCleanup
    {
        NotRequired,
        Complete,
        Failed
    }

    public enum RebindStatus
    {
        Rejected,
        Cancelled,
        Failed,
        Applied
    }

    public enum RebindRejection
    {
        None,
        HostClosed,
        Reentrant,
        Busy,
        SourceUnavailable,
        RouteMismatch,
        InputControlUnsupported,
        /// <summary>此实例仍由其他父页面共享，不能替换其模型身份。</summary>
        InUse
    }

    /// <summary>
    /// 换绑结果。Applied 表示新模型已提交；之后旧模型释放失败不会撤销提交。
    /// RecoveryFailed 表示绑定、Presenter 或输入无法安全恢复，宿主必须关闭且禁止缓存。
    /// </summary>
    public readonly struct RebindOutcome
    {
        internal RebindOutcome(RebindStatus status, RebindRejection rejection = RebindRejection.None,
                    Exception error = null, bool recoveryFailed = false, RebindCleanup cleanup = RebindCleanup.NotRequired)
        {
            Status = status;
            Rejection = rejection;
            Error = error;
            RecoveryFailed = recoveryFailed;
            Cleanup = cleanup;
        }

        public RebindStatus Status
        {
            get;
        }

        public RebindRejection Rejection
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        public bool RecoveryFailed
        {
            get;
        }

        public RebindCleanup Cleanup
        {
            get;
        }

        public bool IsApplied => Status == RebindStatus.Applied;
    }
}
