using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        public ValueTask<OpenOutcome<TResult>> OpenAsync<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
                    TArgs args, CancellationToken cancellationToken = default, TViewModel assignedViewModel = null)
                    where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            var trace = BeginOperationTrace(route.Key, "OpenAsync");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = OpenAsyncUntraced(route, args, cancellationToken, assignedViewModel, trace);
                    // 未启用追踪时直接归还原操作，不增加异步状态机或重复消费 ValueTask。
                    return trace.Id == 0 ? operation : ObserveTracedOpenAsync(operation, trace);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "抛出异常", error);
                throw;
            }
        }

        private void FinishOperationTrace<TResult>(NavigationTraceOperation operation, OpenOutcome<TResult> result)
        {
            if (operation.Id == 0 || !traceRecording || !ReferenceEquals(operation.Session, traceSession))
            {
                return;
            }
            FinishOperationTrace(operation, result.Handle.Identity,
                result.Status + "/" + result.Rejection + "/" + result.Cleanup, result.Error, result.ReplacedHandle);
        }

        private ValueTask<OpenOutcome<TResult>> ObserveTracedOpenAsync<TResult>(ValueTask<OpenOutcome<TResult>> operation,
                    NavigationTraceOperation trace) => ObserveTracedOperationAsync(operation, trace, FinishOperationTrace);
    }
}
