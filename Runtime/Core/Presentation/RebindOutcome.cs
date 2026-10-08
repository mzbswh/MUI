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
        /// <summary>旧绑定不支持在换绑提交时同步切断订阅。</summary>
        BindingDetachUnsupported,
        /// <summary>此实例仍由其他父页面共享，不能替换其模型身份。</summary>
        InUse
    }

    /// <summary>
    /// 换绑结果。Applied 表示新模型已提交；之后旧模型释放失败不会撤销提交。
    /// ViewFaulted 表示提交或输入清理失败，宿主必须故障关闭且禁止缓存，不承诺回滚业务副作用。
    /// </summary>
    public readonly struct RebindOutcome
    {
        internal RebindOutcome(RebindStatus status, RebindRejection rejection = RebindRejection.None,
                    Exception error = null, bool viewFaulted = false, RebindCleanup cleanup = RebindCleanup.NotRequired)
        {
            Status = status;
            Rejection = rejection;
            Error = error;
            ViewFaulted = viewFaulted;
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

        /// <summary>与错误出口共享的诊断标识；无异常时为 Guid.Empty。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        public bool ViewFaulted
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
