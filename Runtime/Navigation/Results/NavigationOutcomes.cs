using System;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public enum OpenStatus
    {
        Rejected,
        Succeeded,
        CancelledBeforeCommit,
        PreparationFailed,
        ActivationFailed,
        ClosedBeforeReady,
        HostClosed
    }

    public enum OpenRejection
    {
        None,
        Busy,
        Reentrant,
        InstanceLimit,
        ConflictingData,
        CloseDenied,
        ConfirmationUnavailable,
        Superseded,
        CleanupCapacity,
        DependencyCycle,
        DependencyLimit,
        DependencyOrderConflict,
        CloseDecisionTimedOut,
        DependencyMissing,
        RenderOrderCapacity
    }

    public readonly struct OpenOutcome<TResult>
    {
        private readonly Task<CloseOutcome> replacedCleanup;

        internal OpenOutcome(OpenStatus status,
                    ViewHandle<TResult> handle = default,
                    OpenRejection rejection = OpenRejection.None,
                    Exception error = null,
                    CleanupStatus cleanup = CleanupStatus.NotRequired,
                    ViewHandle replacedHandle = default,
                    Task<CloseOutcome> replacedCleanup = null)
        {
            Status = status;
            Handle = handle;
            Rejection = rejection;
            Error = error;
            Cleanup = cleanup;
            ReplacedHandle = replacedHandle;
            this.replacedCleanup = replacedCleanup;
            Readiness = handle.IsValid ? handle.Readiness : default;
        }

        public OpenStatus Status
        {
            get;
        }

        public ViewHandle<TResult> Handle
        {
            get;
        }

        public OpenRejection Rejection
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        /// <summary>与错误出口共享的诊断标识；无异常时为 Guid.Empty。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        public CleanupStatus Cleanup
        {
            get;
        }

        /// <summary>仅超限替换提交后设置，即使目标随后激活失败。</summary>
        public ViewHandle ReplacedHandle
        {
            get;
        }

        /// <summary>超限替换提交后，等待被替换页面的清理结果；未替换时为 null。</summary>
        public Task<CloseOutcome> ReplacedCleanup => replacedCleanup;

        public bool IsReplacement => ReplacedHandle.IsValid;

        /// <summary>返回时的快照；后续变化应读取 Handle.Readiness 或等待句柄。</summary>
        public ViewReadiness Readiness
        {
            get;
        }

        public bool IsSuccess => Status == OpenStatus.Succeeded;
    }

    public enum CloseStatus
    {
        NotFound,
        Closed,
        AlreadyClosed,
        WaitCancelled,
        Failed,
        UnknownOrExpired,
        Reentrant,
        Blocked,
        Handled,
        Denied,
        ConfirmationUnavailable,
        Superseded,
        AlreadyClosing,
        ClosedWithCleanupPending,
        /// <summary>界面仍被其他页面持有，普通关闭不会撤销其所有权。</summary>
        InUse,
        /// <summary>同一激活正在确认另一个关闭意图；没有接纳本次请求。</summary>
        Busy
    }

    public readonly struct CloseOutcome
    {
        internal CloseOutcome(CloseStatus status, Exception error = null, CleanupStatus cleanup = CleanupStatus.Complete)
        {
            Status = status;
            Error = error;
            Cleanup = cleanup;
        }

        public CloseStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        /// <summary>与错误出口共享的诊断标识；无异常时为 Guid.Empty。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        public CleanupStatus Cleanup
        {
            get;
        }
    }

    public enum ViewState
    {
        Unknown,
        Opening,
        Open,
        Closing,
        Destroyed,
        Failed,
        UnknownOrExpired
    }

    public enum PostOpenStatus
    {
        Accepted,
        Busy,
        HostClosed
    }
}
