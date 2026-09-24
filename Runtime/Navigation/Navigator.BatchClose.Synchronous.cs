using System;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>同步关闭本层页面快照，按层级与显示顺序从前到后执行。</summary>
        public BatchCloseOutcome CloseLayer(int layer) => CloseBatchTraced(layer);

        /// <summary>同步关闭全部已提交页面快照；守卫拒绝或清理失败不阻止后续项。</summary>
        public BatchCloseOutcome CloseAll() => CloseBatchTraced(null);

        private BatchCloseOutcome CloseBatchSynchronous(int? layer)
        {
            RequireSynchronousNavigation();
            if (IsReentrant || HasCloseEvaluation)
            {
                return new BatchCloseOutcome(BatchCloseStatus.Reentrant, Array.Empty<BatchCloseItem>());
            }

            if (activeCloseBatches >= queueCapacity || pending != 0)
            {
                return new BatchCloseOutcome(BatchCloseStatus.Busy, Array.Empty<BatchCloseItem>());
            }

            var snapshot = CaptureCloseBatch(layer);
            // 整批操作前拒绝自身命令，不允许先关闭前几项再发现需要等待调用者退出。
            foreach (var instance in snapshot)
            {
                if (WouldWaitForSelf(instance.Handle) || instance.IsExecutingBindingCommand)
                {
                    return new BatchCloseOutcome(BatchCloseStatus.Reentrant, Array.Empty<BatchCloseItem>());
                }
            }

            var results = CreateCloseBatchItems(snapshot);
            ++activeCloseBatches;
            try
            {
                for (var index = 0; index < snapshot.Length; ++index)
                {
                    var instance = snapshot[index];
                    CloseOutcome outcome;
                    try
                    {
                        // 前一项的回调可能已关闭本项并淘汰历史；读取实例终态，无需等待任务。
                        outcome = instance.CompletedCloseOutcome ??
                            BeginRequestedCloseSynchronous(instance, DismissReason.Closed);
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                        outcome = new CloseOutcome(CloseStatus.Failed, error, CleanupStatus.Failed);
                    }

                    results[index] = new BatchCloseItem(instance.Handle, outcome);
                }

                return new BatchCloseOutcome(BatchCloseStatus.Completed, results);
            }
            finally
            {
                --activeCloseBatches;
            }
        }
    }
}
