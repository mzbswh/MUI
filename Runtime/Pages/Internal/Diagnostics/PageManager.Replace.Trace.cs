using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        public ValueTask<ReplaceOutcome<TResult>> ReplaceAsync<TViewModel, TArgs, TResult>(PageHandle source,
            PageDefinition<TViewModel, TArgs, TResult> route, TArgs args,
            CancellationToken cancellationToken = default, TViewModel assignedViewModel = null)
            where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            var trace = BeginOperationTrace(route.Key, "ReplaceAsync", source);
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = ReplaceAsyncUntraced(source, route, args, cancellationToken, assignedViewModel, trace);
                    return trace.Id == 0 ? operation : ObserveTracedReplacementAsync(operation, trace);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "抛出异常", error);
                throw;
            }
        }

        private void FinishReplacementTrace<TResult>(PageTraceOperation trace, ReplaceOutcome<TResult> result)
        {
            if (trace.Id == 0 || !traceBuffer.IsRecording || !ReferenceEquals(trace.Session, traceBuffer.Session))
            {
                return;
            }
            var destination = result.Destination;
            var outcome = result.Status + "/" + result.Rejection + ";目标=" + destination.Status +
                "/" + destination.Rejection + "/" + destination.Cleanup;
            var error = destination.Error;
            // 只采集已经成功返回的清理结果，诊断不延迟替换完成。
            if (result.SourceCleanup != null && result.SourceCleanup.IsCompletedSuccessfully)
            {
                var source = result.SourceCleanup.Result;
                outcome += ";源=" + source.Status + "/" + source.Cleanup;
                if (error == null)
                {
                    error = source.Error;
                }
            }
            else if (result.IsCommitted)
            {
                outcome += ";源清理=未采集（不额外等待）";
            }
            FinishOperationTrace(trace, destination.Handle.Identity, outcome, error);
        }

        private ValueTask<ReplaceOutcome<TResult>> ObserveTracedReplacementAsync<TResult>(ValueTask<ReplaceOutcome<TResult>> operation,
            PageTraceOperation trace) => ObserveTracedOperationAsync(operation, trace, FinishReplacementTrace);
    }
}
