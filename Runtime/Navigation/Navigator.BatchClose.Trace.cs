using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private static string GetBatchTraceName(int? layer) =>
            (layer.HasValue ? "CloseLayer" : "CloseAll") + "Async" +
            (layer.HasValue ? "（层=" + layer.Value + "）" : string.Empty);

        private ValueTask<BatchCloseOutcome> CloseBatchTracedAsync(int? layer, CancellationToken token)
        {
            AssertThread();
            var trace = traceRecording ? BeginOperationTrace(string.Empty, GetBatchTraceName(layer)) : default;
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = BeginCloseBatch(layer, token);
                    return trace.Id == 0 ? operation : ObserveTracedOperationAsync(operation, trace, FinishBatchCloseTrace);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "抛出异常", error);
                throw;
            }
        }

        private void FinishBatchCloseTrace(NavigationTraceOperation trace, BatchCloseOutcome result)
        {
            if (trace.Id == 0 || !traceRecording || !ReferenceEquals(trace.Session, traceSession))
            {
                return;
            }
            // 为最终汇总保留一格；复制工作量不随超大批次无界增长，省略数明确写入汇总。
            var start = Math.Max(0, result.Items.Count - (traceEntries.Length - 1));
            for (var i = start; i < result.Items.Count; ++i)
            {
                AppendTrace(new NavigationTraceEntry(host, trace.Id, trace.Name, result.Items[i], i));
            }
            FinishOperationTrace(trace, default, result.Status + ";全部关闭=" + result.AllClosed +
                ";总项数=" + result.Items.Count + ";逐项记录省略=" + start, null);
        }
    }
}
