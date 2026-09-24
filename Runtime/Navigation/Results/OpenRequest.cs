using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    /// <summary>
    /// 控制一次打开操作，不代表尚未提交的 View。Completion 可重复等待，
    /// 提交前取消请求回滚，不发出 Close 请求。
    /// 操作结束后自动释放其拥有的取消源。
    /// </summary>
    public sealed class OpenRequest<TResult>
    {
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private CancellationTokenSource cancellation;
        private bool cancelling;
        private bool releasePending;

        internal OpenRequest(Func<CancellationToken, ValueTask<OpenOutcome<TResult>>> open, CancellationToken token)
        {
            OperationId = Guid.NewGuid();
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                Completion = CompleteAsync(open(cancellation.Token));
            }
            catch
            {
                ReleaseCancellation();
                throw;
            }
        }

        public Guid OperationId
        {
            get;
        }

        public Task<OpenOutcome<TResult>> Completion
        {
            get;
        }

        /// <summary>
        /// 在创建时的 UI 线程调用。发出取消信号时返回 true，
        /// 实际结果需查看 Completion；已完成或已取消时返回 false。
        /// </summary>
        public bool Cancel()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("Cancel an OpenRequest on its owning UI thread, or cancel its supplied external token.");
            }

            var current = cancellation;
            if (current == null || releasePending || current.IsCancellationRequested)
            {
                return false;
            }

            cancelling = true;
            try
            {
                current.Cancel(throwOnFirstException: false);
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
            finally
            {
                cancelling = false;
                if (releasePending)
                {
                    ReleaseCancellation();
                }
            }

            return true;
        }

        private async Task<OpenOutcome<TResult>> CompleteAsync(ValueTask<OpenOutcome<TResult>> operation)
        {
            try
            {
                return await operation;
            }
            finally
            {
                ReleaseCancellation();
            }
        }

        private void ReleaseCancellation()
        {
            // 提供方可能在取消回调内同步完成，应等 Cancel 返回后再释放。
            if (cancelling)
            {
                releasePending = true;
                return;
            }

            var current = cancellation;
            cancellation = null;
            releasePending = false;
            if (current != null)
            {
                current.Dispose();
            }
        }
    }
}
