using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private NavigationTraceOperation BeginHandleOperationTrace(ViewHandle handle, string name)
        {
            if (!traceRecording)
            {
                return default;
            }
            // 仅读取账本中的路由键；未知或已过期句柄保持为空，不调用项目或渲染器。
            var key = entries.TryGetValue(handle, out var instance) ? instance.Route.Key : string.Empty;
            return BeginOperationTrace(key, name, handle);
        }

        private CloseOutcome CloseSynchronousTraced(ViewHandle handle, bool force,
            Func<ViewInstance, Action> createCompletion, string name)
        {
            AssertThread();
            var trace = BeginHandleOperationTrace(handle, name);
            try
            {
                var result = CloseSynchronousCore(handle, force, createCompletion);
                FinishCloseTrace(trace, result);
                return result;
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, handle, "抛出异常", error);
                throw;
            }
        }

        private ValueTask<CloseOutcome> CloseAsyncTraced(ViewHandle handle, bool force, CancellationToken token)
        {
            AssertThread();
            RequireAsyncNavigation();
            var trace = BeginHandleOperationTrace(handle, force ? "ForceCloseAsync" : "CloseAsync");
            try
            {
                var operation = CloseAsyncCore(handle, force, token);
                return trace.Id == 0 ? operation : ObserveTracedCloseAsync(operation, trace);
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, handle, "抛出异常", error);
                throw;
            }
        }

        /// <summary>记录结果完成请求，但不把业务结果值写入追踪。</summary>
        public ValueTask<CloseOutcome> CompleteAsync<TResult>(ViewHandle<TResult> handle, TResult result,
            CancellationToken cancellationToken = default)
        {
            AssertThread();
            RequireAsyncNavigation();
            var trace = BeginHandleOperationTrace(handle.Identity, "CompleteAsync");
            try
            {
                var operation = CompleteAsyncUntraced(handle, result, cancellationToken);
                return trace.Id == 0 ? operation : ObserveTracedCloseAsync(operation, trace);
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, handle.Identity, "抛出异常", error);
                throw;
            }
        }

        /// <summary>仅显式请求时等待真实清理；追踪记录等待结果，不自动发起第二次清理。</summary>
        public ValueTask<CloseOutcome> WaitForCleanupAsync(ViewHandle handle, CancellationToken cancellationToken = default)
        {
            AssertThread();
            RequireAsyncNavigation();
            var trace = BeginHandleOperationTrace(handle, "WaitForCleanupAsync");
            try
            {
                var operation = WaitForCleanupUntracedAsync(handle, cancellationToken);
                return trace.Id == 0 ? operation : ObserveTracedCloseAsync(operation, trace);
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, handle, "抛出异常", error);
                throw;
            }
        }

        private void FinishCloseTrace(NavigationTraceOperation trace, CloseOutcome result)
        {
            if (trace.Id == 0 || !traceRecording || !ReferenceEquals(trace.Session, traceSession))
            {
                return;
            }
            // Pending 是仍有清理责任，不可因请求已经返回就记录为完成释放。
            FinishOperationTrace(trace, trace.Source, result.Status + "/" + result.Cleanup, result.Error);
        }

        private ValueTask<CloseOutcome> ObserveTracedCloseAsync(ValueTask<CloseOutcome> operation,
            NavigationTraceOperation trace) => ObserveTracedOperationAsync(operation, trace, FinishCloseTrace);
    }
}
