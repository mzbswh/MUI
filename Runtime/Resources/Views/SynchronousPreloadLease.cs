using System;

namespace MUI.Resources
{
    /// <summary>同步驻留凭证；释放委托只执行一次，失败结果在重复释放时继续报告。</summary>
    public sealed class SynchronousPreloadLease : ISynchronousPreloadLease
    {
        private readonly int thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private Action release;
        private bool releasing;
        private bool released;
        private Exception failure;

        public SynchronousPreloadLease(ViewResource resource, Action release)
        {
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            this.release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public ViewResource Resource
        {
            get;
        }

        public void Dispose()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != thread || releasing)
            {
                throw new InvalidOperationException("Preload release requires its owning thread and cannot reenter itself.");
            }
            if (!released)
            {
                releasing = true;
                var callback = release;
                release = null;
                try
                {
                    callback();
                }
                catch (Exception error)
                {
                    failure = error;
                }
                finally
                {
                    releasing = false;
                    released = true;
                }
            }
            if (failure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
