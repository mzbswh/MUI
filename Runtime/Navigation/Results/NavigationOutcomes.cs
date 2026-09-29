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
        RequiresAsync,
        RequiresPreload,
        SyncCreationUnsupported,
        InstanceLimit,
        ConflictingData,
        CloseDenied,
        ConfirmationUnavailable,
        Superseded,
        CleanupCapacity,
        DependencyCycle,
        DependencyLimit,
        DependencyOrderConflict,
        CloseDecisionTimedOut
    }

    public readonly struct OpenOutcome<TResult>
    {
        private readonly Task<CloseOutcome> replacedCleanup;
        private readonly ViewCompletion<CloseOutcome> synchronousReplacedCleanup;

        internal OpenOutcome(OpenStatus status,
                    ViewHandle<TResult> handle = default,
                    OpenRejection rejection = OpenRejection.None,
                    Exception error = null,
                    CleanupStatus cleanup = CleanupStatus.NotRequired,
                    ViewHandle replacedHandle = default,
                    Task<CloseOutcome> replacedCleanup = null,
                    CloseOutcome? replacedClose = null,
                    ViewCompletion<CloseOutcome> synchronousReplacedCleanup = null)
        {
            Status = status;
            Handle = handle;
            Rejection = rejection;
            Error = error;
            Cleanup = cleanup;
            ReplacedHandle = replacedHandle;
            this.replacedCleanup = replacedCleanup;
            this.synchronousReplacedCleanup = synchronousReplacedCleanup;
            ReplacedClose = replacedClose;
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

        public CleanupStatus Cleanup
        {
            get;
        }

        /// <summary>仅超限替换提交后设置，即使目标随后激活失败。</summary>
        public ViewHandle ReplacedHandle
        {
            get;
        }

        /// <summary>兼容异步等待；同步超限替换仅在显式读取本属性时创建任务。</summary>
        public Task<CloseOutcome> ReplacedCleanup => replacedCleanup ??
            (synchronousReplacedCleanup == null ? null : synchronousReplacedCleanup.Task);

        /// <summary>同步超限替换的旧页面清理结果。</summary>
        public CloseOutcome? ReplacedClose
        {
            get;
        }

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
        RequiresAsync,
        /// <summary>界面仍被其他页面持有，普通关闭不会撤销其所有权。</summary>
        InUse
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
