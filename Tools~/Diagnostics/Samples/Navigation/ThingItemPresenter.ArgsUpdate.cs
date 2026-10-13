using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    public sealed partial class ThingItemPresenter
    {
        private int outstandingCandidates;

        public int CandidatesDisposed
        {
            get; private set;
        }

        public bool ClosedDuringArgsUpdate
        {
            get; private set;
        }

        public TaskCompletionSource<bool> ArgsRelease
        {
            get;
        } =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override void OnClose()
        {
            ClosedDuringArgsUpdate |= outstandingCandidates != 0;
        }

        public async ValueTask<IPreparedArgsUpdate> PrepareArgsUpdateAsync(string args, CancellationToken cancellationToken)
        {
            if (args == "Slow")
            {
                // 不合作候选用于观察停用是否先排空更新、后执行 OnClose。
                await ArgsRelease.Task;
            }
            else
            {
                await Task.Delay(10, cancellationToken);
            }

            ++outstandingCandidates;
            return new ItemArgsCandidate(this, ViewModel, args + " × 20");
        }

        private sealed class ItemArgsCandidate : IPreparedArgsUpdate
        {
            private ThingItemPresenter owner;
            private ThingItemViewModel model;
            private readonly string nextLabel;

            internal ItemArgsCandidate(ThingItemPresenter owner, ThingItemViewModel model, string nextLabel)
            {
                this.owner = owner;
                this.model = model;
                this.nextLabel = nextLabel;
            }

            public void Commit()
            {
                model.Label = nextLabel;
            }

            public ValueTask DisposeAsync()
            {
                if (owner != null)
                {
                    --owner.outstandingCandidates;
                    ++owner.CandidatesDisposed;
                    owner = null;
                    model = null;
                }

                return default;
            }
        }
    }
}
