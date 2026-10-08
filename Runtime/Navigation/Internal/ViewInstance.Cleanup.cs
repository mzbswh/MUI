using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    internal abstract partial class ViewInstance
    {
        /// <summary>真实清理完成信号，不随逻辑关闭超时提前完成。</summary>
        private Task<CloseOutcome> cleanupCompletion;
        private CancellationTokenSource cleanupCancellation;
        private Exception cleanupCancellationFailure;

        internal Task<CloseOutcome> CleanupCompletion
        {
            get => cleanupCompletion;
            set => cleanupCompletion = value;
        }

        internal bool CleanupTimedOut
        {
            get; set;
        }

        /// <summary>真实清理终态；批次保留实例引用时不依赖有界历史，也无需读取任务。</summary>
        internal CloseOutcome? CompletedCloseOutcome
        {
            get; set;
        }

        protected CancellationToken CleanupToken => cleanupCancellation == null ? CancellationToken.None : cleanupCancellation.Token;

        internal Exception CleanupCancellationFailure => cleanupCancellationFailure;

        internal void BeginCleanup() => cleanupCancellation = new CancellationTokenSource();

        internal void CancelCleanup()
        {
            try
            {
                cleanupCancellation.Cancel();
            }
            catch (Exception error)
            {
                cleanupCancellationFailure = error;
            }
        }

        internal void EndCleanup()
        {
            cleanupCancellation.Dispose();
            cleanupCancellation = null;
        }

        internal abstract void PublishCloseResult(DismissReason reason, Exception error, CleanupStatus cleanup, bool faulted = false);
    }
}
