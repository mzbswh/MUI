using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>同步预加载与诊断，复用占位不会发起第二次资源加载。</summary>
        public PreloadOutcome Preload(Route route)
        {
            RequireSynchronousNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            var trace = BeginOperationTrace(route.Key, "Preload");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var result = PreloadUntraced(route);
                    FinishPreloadTrace(trace, result);
                    return result;
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "抛出异常", error);
                throw;
            }
        }

        public ValueTask<PreloadOutcome> PreloadAsync(Route route, CancellationToken cancellationToken = default)
        {
            AssertThread();
            RequireAsyncNavigation();
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

        private void FinishPreloadTrace(NavigationTraceOperation trace, PreloadOutcome result)
        {
            if (trace.Id == 0 || !traceRecording || !ReferenceEquals(trace.Session, traceSession))
            {
                return;
            }
            FinishOperationTrace(trace, default, result.Status + ";复用预加载占位=" + result.ReusedReservation, result.Error);
        }
    }
}
