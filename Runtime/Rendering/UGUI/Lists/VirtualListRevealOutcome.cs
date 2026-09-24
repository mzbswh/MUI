using System;

namespace MUI.UGUI
{
    public enum VirtualListRevealStatus
    {
        Ready,
        NotFound,
        Superseded,
        Inactive,
        Cancelled,
        InputBlocked,
        NoSelectable,
        Failed
    }

    public readonly struct VirtualListRevealOutcome
    {
        internal VirtualListRevealOutcome(VirtualListRevealStatus status, Exception error = null)
        {
            Status = status;
            Error = error;
        }

        public VirtualListRevealStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }
    }
}
