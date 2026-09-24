using System;

namespace MUI.Samples.Navigation
{
    public enum PageResetStatus
    {
        Applied,
        Superseded,
        WaitCancelled,
        Inactive,
        Failed
    }

    /// <summary>查询重置结果；Applied 只表示新查询已就绪，第一页仍需显式加载或自动预取。</summary>
    public readonly struct PageResetResult
    {
        public PageResetResult(PageResetStatus status, Exception error = null)
        {
            if (!Enum.IsDefined(typeof(PageResetStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            Error = error;
        }

        public PageResetStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }
    }
}
