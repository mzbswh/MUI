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
        Failed,
        /// <summary>位置快照来自不同来源，且调用方未声明键空间兼容。</summary>
        IncompatibleSource
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
