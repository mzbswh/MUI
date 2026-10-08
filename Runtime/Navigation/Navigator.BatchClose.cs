using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private int activeCloseBatches;

        /// <summary>对调用时已提交的本层页面按从前到后顺序关闭，并执行正常关闭守卫。</summary>
        public ValueTask<BatchCloseOutcome> CloseLayerAsync(int layer, CancellationToken cancellationToken = default) => CloseBatchTracedAsync(layer, cancellationToken);

        /// <summary>关闭已提交页面的快照，不停止宿主，也不取消准备中的打开请求。</summary>
        public ValueTask<BatchCloseOutcome> CloseAllAsync(CancellationToken cancellationToken = default) => CloseBatchTracedAsync(null, cancellationToken);

        private ValueTask<BatchCloseOutcome> BeginCloseBatch(int? layer, CancellationToken token)
        {
            AssertThread();
            if (IsReentrant || HasCloseEvaluation)
            {
                return BatchRejected(BatchCloseStatus.Reentrant);
            }

            if (activeCloseBatches >= queueCapacity)
            {
                return BatchRejected(BatchCloseStatus.Busy);
            }

            var snapshot = CaptureCloseBatch(layer);
            // 若操作会等待自身来源，必须在修改任何状态前拒绝整个批次。
            foreach (var instance in snapshot)
            {
                if (WouldWaitForSelf(instance.Handle))
                {
                    return BatchRejected(BatchCloseStatus.Reentrant);
                }
            }

            activeCloseBatches++;
            return new ValueTask<BatchCloseOutcome>(CloseBatchAsync(snapshot, token));
        }

        private ViewInstance[] CaptureCloseBatch(int? layer) =>
                    ownership.OrderCloseBatch(entries.Values
                        .Where(instance => instance.Order != 0 &&
                            (instance.State == ViewState.Open || instance.State == ViewState.Closing) &&
                            (!layer.HasValue || instance.Route.Policy.Layer == layer.Value))
                        .OrderByDescending(instance => instance.Route.Policy.Layer)
                        .ThenByDescending(instance => instance.Order)
                        .ToArray());

        private static BatchCloseItem[] CreateCloseBatchItems(ViewInstance[] snapshot)
        {
            var results = new BatchCloseItem[snapshot.Length];
            for (var i = 0; i < snapshot.Length; i++)
            {
                results[i] = new BatchCloseItem(snapshot[i].Handle, null);
            }
            return results;
        }

        private static ValueTask<BatchCloseOutcome> BatchRejected(BatchCloseStatus status) => new ValueTask<BatchCloseOutcome>(new BatchCloseOutcome(status, Array.Empty<BatchCloseItem>()));

        private async Task<BatchCloseOutcome> CloseBatchAsync(ViewInstance[] snapshot, CancellationToken token)
        {
            var results = CreateCloseBatchItems(snapshot);

            try
            {
                for (var i = 0; i < snapshot.Length; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        return new BatchCloseOutcome(BatchCloseStatus.WaitCancelled, results);
                    }

                    var instance = snapshot[i];
                    CloseOutcome outcome;
                    try
                    {
                        // 保留原实例，不重新查询容量有限的终态历史。
                        // 较早的关闭回调可能已关闭该项，并使其历史记录被淘汰。
                        var closing = instance.Closing ?? BeginRequestedClose(instance, DismissReason.Closed);
                        outcome = await WaitForCloseCommitAsync(instance, closing, token);
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                        outcome = new CloseOutcome(CloseStatus.Failed, error, CleanupStatus.Failed);
                    }

                    AssertThread();
                    results[i] = new BatchCloseItem(instance.Handle, outcome);
                    if (outcome.Status == CloseStatus.WaitCancelled)
                    {
                        return new BatchCloseOutcome(BatchCloseStatus.WaitCancelled, results);
                    }
                }

                return new BatchCloseOutcome(BatchCloseStatus.Completed, results);
            }
            finally
            {
                activeCloseBatches--;
            }
        }
    }
}
