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

        public CleanupStatus Cleanup
        {
            get;
        }

        public bool IsCompleted => Status == ViewResultStatus.Completed;
    }
}
