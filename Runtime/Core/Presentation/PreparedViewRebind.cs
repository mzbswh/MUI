using System;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>在现有 View 上准备的换绑；提交同步完成，异步收尾由 Completion 追踪。</summary>
    internal sealed class PreparedViewRebind : IAsyncDisposable
    {
        private readonly Action validate;
        private readonly Action commit;
        private readonly Action abort;
        private bool committed;
        private bool disposed;

        internal PreparedViewRebind(Action validate, Action commit, Action abort, Task<RebindOutcome> completion)
        {
            this.validate = validate;
            this.commit = commit;
            this.abort = abort;
            Completion = completion;
        }

        internal Task<RebindOutcome> Completion
        {
            get;
        }

        internal void Validate()
        {
            if (disposed || committed)
            {
                throw new InvalidOperationException("Prepared view rebind is no longer available.");
            }

            validate();
        }

        internal void Commit()
        {
            Validate();
            committed = true;
            commit();
        }

        public async ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                disposed = true;
                if (!committed)
                {
                    abort();
                }
            }

            var outcome = await Completion;
            if (outcome.Cleanup == RebindCleanup.Failed)
            {
                throw outcome.Error ?? new InvalidOperationException("Staged view rebind cleanup failed.");
            }
        }
    }
}
