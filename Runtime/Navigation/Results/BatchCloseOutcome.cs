using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    public enum BatchCloseStatus
    {
        Completed,
        WaitCancelled,
        Reentrant,
        Busy
    }

    public readonly struct BatchCloseItem
    {
        internal BatchCloseItem(ViewHandle handle, CloseOutcome? outcome)
        {
            Handle = handle;
            Outcome = outcome;
        }

        public ViewHandle Handle
        {
            get;
        }

        /// <summary>若取消在请求本项前停止批次，则为 null。</summary>
        public CloseOutcome? Outcome
        {
            get;
        }
    }

    /// <summary>不可变的批次快照结果；Completed 不代表每个页面都同意关闭。</summary>
    public sealed class BatchCloseOutcome
    {
        internal BatchCloseOutcome(BatchCloseStatus status, BatchCloseItem[] items)
        {
            Status = status;
            Items = Array.AsReadOnly(items);
            var allClosed = status == BatchCloseStatus.Completed;
            foreach (var item in items)
            {
                allClosed &= item.Outcome.HasValue && (item.Outcome.Value.Status == CloseStatus.Closed || item.Outcome.Value.Status == CloseStatus.AlreadyClosed) && item.Outcome.Value.Cleanup != CleanupStatus.Failed && item.Outcome.Value.Cleanup != CleanupStatus.Pending;
            }

            AllClosed = allClosed;
        }

        public BatchCloseStatus Status
        {
            get;
        }

        public IReadOnlyList<BatchCloseItem> Items
        {
            get;
        }

        public bool AllClosed
        {
            get;
        }
    }
}
