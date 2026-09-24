using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>按真实返回策略选择目标；局部处理或阻挡时不伪造关闭句柄。</summary>
        public CloseOutcome Back()
        {
            RequireSynchronousNavigation();
            var trace = BeginOperationTrace(string.Empty, "Back");
            try
            {
                var outcome = FindBackTarget(out var target);
                trace = ResolveBackTraceTarget(trace, target);
                var result = target == null ? outcome : BeginRequestedCloseSynchronous(target, DismissReason.Back);
                FinishCloseTrace(trace, result);
                return result;
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, trace.Source, "抛出异常", error);
                throw;
            }
        }

        public ValueTask<CloseOutcome> BackAsync(CancellationToken cancellationToken = default)
        {
            AssertThread();
            RequireAsyncNavigation();
            var trace = BeginOperationTrace(string.Empty, "BackAsync");
            try
            {
                var outcome = FindBackTarget(out var target);
                trace = ResolveBackTraceTarget(trace, target);
                var operation = target == null ? new ValueTask<CloseOutcome>(outcome)
                    : new ValueTask<CloseOutcome>(WaitForCloseAsync(BeginRequestedClose(target, DismissReason.Back), cancellationToken));
                return trace.Id == 0 ? operation : ObserveTracedCloseAsync(operation, trace);
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, trace.Source, "抛出异常", error);
                throw;
            }
        }

        private static NavigationTraceOperation ResolveBackTraceTarget(NavigationTraceOperation trace, ViewInstance target)
        {
            if (trace.Id == 0 || target == null)
            {
                return trace;
            }
            var key = target.Route.Key;
            return new NavigationTraceOperation(trace.Session, trace.Id, trace.Name,
                key.Length > 256 ? key.Substring(0, 256) + "…" : key, trace.Timestamp, target.Handle);
        }
    }
}
