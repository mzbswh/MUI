using System;

namespace MUI
{
    public enum ArgsUpdateCleanup
    {
        NotRequired,
        Complete,
        Failed
    }

    public enum ArgsUpdateStatus
    {
        Rejected,
        CancelledBeforeCommit,
        PreparationFailed,
        CommitFailed,
        Applied
    }

    public enum ArgsUpdateRejection
    {
        None,
        HostClosed,
        Reentrant,
        Busy,
        SourceUnavailable,
        RouteMismatch,
        Unsupported,
        InputControlUnsupported,
        /// <summary>其他父页面仍持有此共享实例。</summary>
        InUse,
        /// <summary>新参数会改变共享依赖契约；本次更新拒绝，项目应显式选择独立变体或重建流程。</summary>
        DependencyChangeRequired
    }

    /// <summary>参数提交与候选清理分别报告；Applied 后的取消或清理失败不会撤销已经提交的参数。</summary>
    public readonly struct ArgsUpdateOutcome
    {
        internal ArgsUpdateOutcome(ArgsUpdateStatus status, ArgsUpdateRejection rejection = ArgsUpdateRejection.None,
                    Exception error = null, ArgsUpdateCleanup cleanup = ArgsUpdateCleanup.NotRequired, bool viewFaulted = false)
        {
            Status = status;
            Rejection = rejection;
            Error = error;
            Cleanup = cleanup;
            ViewFaulted = viewFaulted;
        }

        public ArgsUpdateStatus Status
        {
            get;
        }

        public ArgsUpdateRejection Rejection
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        /// <summary>与错误出口共享的诊断标识；无异常时为 Guid.Empty。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        public ArgsUpdateCleanup Cleanup
        {
            get;
        }

        /// <summary>提交或输入恢复失败，实例进入故障关闭，不能继续复用。</summary>
        public bool ViewFaulted
        {
            get;
        }

        public bool IsApplied => Status == ArgsUpdateStatus.Applied;
    }
}
