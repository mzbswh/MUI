using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>显式参数更新示例，候选准备不修改当前页面，部分提交失败时故障关闭。</summary>
    public sealed class ArgsUpdatePagePresenter : PagePresenter, IArgsUpdatePresenter<PageArgs>
    {
        public TaskCompletionSource<bool> SlowRelease
        {
            get;
        } =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public int OpenCount
        {
            get; private set;
        }

        public int CandidatesDisposed
        {
            get; private set;
        }

        public PageArgs CurrentArgs => Context.Args;

        protected override void OnOpen(PageArgs args)
        {
            ++OpenCount;
            base.OnOpen(args);
        }

        protected override void OnClose()
        {
            Debug.Log($"MUI 参数更新关闭回调：已释放候选={CandidatesDisposed}");
            base.OnClose();
        }

        public async ValueTask<IPreparedArgsUpdate> PrepareArgsUpdateAsync(PageArgs args, CancellationToken cancellationToken)
        {
            if (args.Title == "Slow")
            {
                // 故意忽略取消，演示迟到候选在关闭回调之前被回收。
                await SlowRelease.Task;
            }
            else
            {
                await Task.Delay(10, cancellationToken);
            }

            if (args.Title == "Prepare failure")
            {
                throw new InvalidOperationException("Sample argument preparation failure.");
            }

            return new Candidate(this, ViewModel, args);
        }

        private sealed class Candidate : IPreparedArgsUpdate
        {
            private ArgsUpdatePagePresenter owner;
            private PageViewModel model;
            private readonly PageArgs next;

            internal Candidate(ArgsUpdatePagePresenter owner, PageViewModel model, PageArgs next)
            {
                this.owner = owner;
                this.model = model;
                this.next = next;
            }

            public void Commit()
            {
                // 只在提交阶段写入模型，异常由框架故障关闭。
                model.Title = next.Title;
                if (next.Title == "Commit failure")
                {
                    throw new InvalidOperationException("Sample failure after a partial argument commit.");
                }

                model.Selection = next.Selection;
            }


            public ValueTask DisposeAsync()
            {
                if (owner != null)
                {
                    ++owner.CandidatesDisposed;
                    owner = null;
                    model = null;
                    if (next.Title == "Cleanup failure")
                    {
                        throw new InvalidOperationException("示例候选资源释放失败。");
                    }
                }

                return default;
            }
        }
    }
}
