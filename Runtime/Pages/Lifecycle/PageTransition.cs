using System;

namespace MUI.Pages
{
    public enum PageTransitionStatus
    {
        Pending,
        Completed,
        Interrupted,
        Failed
    }

    /// <summary>一次视觉阶段的不可变结果，不代表页面业务结果或资源清理完成。</summary>
    public readonly struct PageTransition
    {
        internal PageTransition(PageTransitionStatus status, Exception error = null)
        {
            Status = status;
            Error = error;
        }

        public PageTransitionStatus Status
        {
            get;
        }

        /// <summary>转场或视觉收尾错误；完成但有错误表示已恢复到可用终态。</summary>
        public Exception Error
        {
            get;
        }

        /// <summary>与错误出口共享的诊断标识；无异常时为 Guid.Empty。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        public bool IsCompleted => Status == PageTransitionStatus.Completed;

        public bool IsDegraded => IsCompleted && Error != null;
    }
}
