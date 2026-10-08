using System;

namespace MUI.Navigation
{
    public enum ViewResultStatus
    {
        Pending,
        Completed,
        Dismissed,
        Faulted
    }

    public enum DismissReason
    {
        Closed,
        Back,
        HostShutdown,
        OpenCancelled,
        OpenFailed,
        Forced,
        Replaced,
        ArgsUpdateFailed,
        RebindFailed
    }

    public enum CleanupStatus
    {
        NotRequired,
        Complete,
        Pending,
        Failed
    }

    public readonly struct ViewResult<TResult>
    {
        internal ViewResult(ViewResultStatus status, TResult value, DismissReason reason, Exception error, CleanupStatus cleanup)
        {
            Status = status;
            Value = value;
            Reason = reason;
            Error = error;
            Cleanup = cleanup;
        }

        public ViewResultStatus Status
        {
            get;
        }

        public TResult Value
        {
            get;
        }

        public DismissReason Reason
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        /// <summary>与错误出口共享的诊断标识；无异常时为 Guid.Empty。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        /// <summary>结果提交时的清理快照；最终归还状态通过 Navigator.WaitForCleanupAsync 单独观察。</summary>
        public CleanupStatus Cleanup
        {
            get;
        }

        public bool IsCompleted => Status == ViewResultStatus.Completed;
    }
}
