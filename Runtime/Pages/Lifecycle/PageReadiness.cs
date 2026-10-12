using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Pages
{
    public enum PageReadinessStatus
    {
        Pending,
        Ready,
        ClosedBeforeReady,
        ActivationFailed,
        WaitCancelled,
        Transitioning
    }

    /// <summary>首次激活的结果，不代表页面当前可见性或输入资格。</summary>
    public readonly struct PageReadiness
    {
        internal PageReadiness(PageReadinessStatus status, Exception error = null, bool degraded = false)
        {
            Status = status;
            Error = error;
            IsDegraded = degraded;
        }

        public PageReadinessStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        /// <summary>与错误出口共享的诊断标识；无异常时为 Guid.Empty。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        public bool IsDegraded
        {
            get;
        }

        public bool IsReady => Status == PageReadinessStatus.Ready;

        internal static async Task<PageReadiness> WaitAsync(Task<PageReadiness> completion, CancellationToken token)
        {
            try
            {
                return await AsyncWait.WithCancellation(completion, token);
            }
            catch (OperationCanceledException)
            {
                return new PageReadiness(PageReadinessStatus.WaitCancelled);
            }
        }
    }
}
