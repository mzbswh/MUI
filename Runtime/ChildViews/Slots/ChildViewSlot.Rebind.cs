using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewSlot
    {
        /// <summary>
        /// 在本槽的队列内换绑当前实例；后续替换、清空或换绑会取消旧请求并等待收尾。
        /// expected 必须在实际执行时仍是当前实例，失效时失败，不自动换绑其他实例。
        /// </summary>
        public ValueTask<ChildViewChangeResult> RebindAsync<TViewModel, TArgs>(
            ChildViewHandle<TViewModel, TArgs> expected, TViewModel next,
            CancellationToken cancellationToken = default) where TViewModel : ViewModel
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            return Enqueue(null, cancellationToken, rebind: token =>
            {
                RequireRebindTarget(expected);
                return expected.RebindAsync(next, token);
            });
        }

        private void RequireRebindTarget(ChildViewHandle expected)
        {
            scope.RequireActive();
            if (stopped || !ReferenceEquals(Current, expected) || !expected.BelongsTo(scope))
            {
                throw new InvalidOperationException("Rebind target is no longer the current child of this slot.");
            }
        }

        private async ValueTask<RebindOutcome> InvokeRebindAsync(Request request, CancellationToken token)
        {
            var previous = preparationFrame.Value;
            var frame = new PreparationFrame();
            preparationFrame.Value = frame;
            try
            {
                // 和准备回调一样阻止自等待；生命周期负责候选清理、提交故障关闭及关闭时排空。
                return await request.Rebind(token);
            }
            finally
            {
                frame.Active = false;
                preparationFrame.Value = previous;
            }
        }

        private static ChildViewChangeResult ToChangeResult(RebindOutcome outcome)
        {
            if (outcome.IsApplied)
            {
                return new ChildViewChangeResult(ChildViewChangeStatus.Ready, outcome.Error);
            }

            if (outcome.Status == RebindStatus.Cancelled)
            {
                return new ChildViewChangeResult(ChildViewChangeStatus.Cancelled, outcome.Error);
            }

            return new ChildViewChangeResult(ChildViewChangeStatus.Failed, outcome.Error ??
                new InvalidOperationException("Child rebind was rejected: " + outcome.Rejection));
        }
    }
}
