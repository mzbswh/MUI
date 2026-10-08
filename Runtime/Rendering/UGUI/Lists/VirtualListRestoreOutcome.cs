using System;

namespace MUI.UGUI
{
    /// <summary>恢复操作结果；回退标志表示未使用快照中的原锚点。</summary>
    public readonly struct VirtualListRestoreOutcome
    {
        internal VirtualListRestoreOutcome(VirtualListRevealOutcome operation, bool usedFallback)
        {
            Status = operation.Status;
            Error = operation.Error;
            UsedFallback = usedFallback;
        }

        public VirtualListRevealStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        public bool UsedFallback
        {
            get;
        }
    }
}
