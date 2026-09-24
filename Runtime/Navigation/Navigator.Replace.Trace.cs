using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>同步替换并记录请求边界，不读取异步源清理属性。</summary>
        public ReplaceOutcome<TResult> Replace<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TArgs args, TViewModel assignedViewModel = null)
            where TViewModel : ViewModel
        {
            RequireSynchronousNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            var trace = BeginOperationTrace(route.Key, "Replace", source);
            try
            {
                var result = ReplaceUntraced(source, route, args, assignedViewModel, trace);
                FinishReplacementTrace(trace, result);
                return result;
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "抛出异常", error);
                throw;
            }
        }

        public ValueTask<ReplaceOutcome<TResult>> ReplaceAsync<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TArgs args,
            CancellationToken cancellationToken = default, TViewModel assignedViewModel = null)
            where TViewModel : ViewModel
        {
            AssertThread();
            RequireAsyncNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            var trace = BeginOperationTrace(route.Key, "ReplaceAsync", source);
            try
            {
                var operation = ReplaceAsyncUntraced(source, route, args, cancellationToken, assignedViewModel, trace);
                return trace.Id == 0 ? operation : ObserveTracedReplacementAsync(operation, trace);
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "抛出异常", error);
                throw;
            }
        }

        private void FinishReplacementTrace<TResult>(NavigationTraceOperation trace, ReplaceOutcome<TResult> result)
        {
            if (trace.Id == 0 || !traceRecording || !ReferenceEquals(trace.Session, traceSession))
            {
                return;
            }
            var destination = result.Destination;
            var outcome = result.Status + "/" + result.Rejection + ";目标=" + destination.Status +
                "/" + destination.Rejection + "/" + destination.Cleanup;
            var error = destination.Error;
            // SourceCleanup 是按需任务接口；诊断不能为纯同步路径创建它，也不能额外等待源清理。
            if (result.SourceClose.HasValue)
            {
                var source = result.SourceClose.Value;
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
            NavigationTraceOperation trace) => ObserveTracedOperationAsync(operation, trace, FinishReplacementTrace);
    }
}
