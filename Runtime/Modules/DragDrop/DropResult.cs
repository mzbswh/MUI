using System;

namespace MUI.DragDrop
{
    /// <summary>最终业务结果；Committed 不会被稍后到达的取消信号覆盖。</summary>
    public enum DropStatus
    {
        Committed,
        Rejected,
        Cancelled,
        Failed
    }

    /// <summary>交互阶段；Completed 在视觉回调前设置，收尾状态通过 IsCompleted/TryGetResult 查询。</summary>
    public enum DragPhase
    {
        Dragging,
        Dropping,
        Completed
    }

    /// <summary>一次拖放的最终结果，不表示拥有载荷或目标资源。</summary>
    public readonly struct DropResult
    {
        internal DropResult(DropStatus status, Exception error = null)
        {
            Status = status;
            Error = error;
        }

        /// <summary>业务提交、拒绝、取消或失败状态。</summary>
        public DropStatus Status
        {
            get;
        }

        /// <summary>失败原因；视觉收尾异常另由 UIErrors 报告，不改写业务结果。</summary>
        public Exception Error
        {
            get;
        }
    }
}
