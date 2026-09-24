using System;

namespace MUI.Navigation
{
    public enum PreloadStatus
    {
        Ready,
        Unsupported,
        CapacityExceeded,
        Cancelled,
        Superseded,
        WaitCancelled,
        HostClosed,
        Reentrant,
        Failed,
        InactiveContentClearing
    }

    public readonly struct PreloadOutcome
    {
        internal PreloadOutcome(PreloadStatus status, Exception error = null, bool reusedReservation = false)
        {
            Status = status;
            Error = error;
            ReusedReservation = reusedReservation;
        }

        /// <summary>本次复用已有预加载占位；可能仍在加载，不表示底层物理缓存命中或已经就绪。</summary>
        public bool ReusedReservation
        {
            get;
        }

        public PreloadStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }
    }
}
