using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public enum ViewReadinessStatus
    {
        Pending,
        Ready,
        ClosedBeforeReady,
        ActivationFailed,
        WaitCancelled,
        Transitioning
    }

    /// <summary>首次激活的结果，不代表页面当前可见性或输入资格。</summary>
    public readonly struct ViewReadiness
    {
        internal ViewReadiness(ViewReadinessStatus status, Exception error = null, bool degraded = false)
        {
            Status = status;
            Error = error;
            IsDegraded = degraded;
        }

        public ViewReadinessStatus Status
        {
            get;
        }

        public Exception Error
        {
            get;
        }

        public bool IsDegraded
        {
            get;
        }

        public bool IsReady => Status == ViewReadinessStatus.Ready;

        internal static async Task<ViewReadiness> WaitAsync(Task<ViewReadiness> completion, CancellationToken token)
        {
            try
            {
                return await AsyncWait.WithCancellation(completion, token);
            }
            catch (OperationCanceledException)
            {
                return new ViewReadiness(ViewReadinessStatus.WaitCancelled);
            }
        }
    }
}
