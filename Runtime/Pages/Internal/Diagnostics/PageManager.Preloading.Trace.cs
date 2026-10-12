using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        public ValueTask<PreloadOutcome> PreloadAsync(PageDefinition route, CancellationToken cancellationToken = default)
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            var trace = BeginOperationTrace(route.Key, "PreloadAsync");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = PreloadAsyncUntraced(route, cancellationToken);
                    return trace.Id == 0 ? operation : ObserveTracedOperationAsync(operation, trace, FinishPreloadTrace);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "抛出异常", error);
                throw;
            }
        }

        private void FinishPreloadTrace(PageTraceOperation trace, PreloadOutcome result)
        {
            if (trace.Id == 0 || !traceBuffer.IsRecording || !ReferenceEquals(trace.Session, traceBuffer.Session))
            {
                return;
            }
            FinishOperationTrace(trace, default, result.Status + ";复用预加载占位=" + result.ReusedReservation, result.Error);
        }
    }
}
