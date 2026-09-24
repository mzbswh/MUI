using System;

namespace MUI.ChildViews
{
    /// <summary>子项选择结果；Ready 表示选择已提交，不表示旧资源已释放。</summary>
    public enum ChildViewChangeStatus
    {
        Ready,
        Empty,
        Superseded,
        Cancelled,
        ParentInactive,
        Failed
    }

    /// <summary>一次替换或清空请求的结果；最终资源收尾需等待槽或句柄清理。</summary>
    public readonly struct ChildViewChangeResult
    {
        internal ChildViewChangeResult(ChildViewChangeStatus status, Exception error = null)
        {
            Status = status;
            Error = error;
        }

        public ChildViewChangeStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }
    }
}
