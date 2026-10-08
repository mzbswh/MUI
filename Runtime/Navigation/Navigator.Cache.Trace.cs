using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>手动维护 UI 实例缓存；定时扫描不占用请求追踪容量。</summary>
        public void RefreshCache()
        {
            AssertThread();
            if (IsShutdown || IsReentrant || IsSourceCommandRunning || HasCloseEvaluation)
            {
                return;
            }

            var trace = BeginOperationTrace(string.Empty, "RefreshCache");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    RefreshCacheUntraced();
                    FinishOperationTrace(trace, default, "完成", null);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "扫描异常", error);
                throw;
            }
        }

        public ValueTask ClearCacheAsync()
        {
            AssertThread();
            var trace = BeginOperationTrace(string.Empty, "ClearCacheAsync");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = ClearCacheAsyncUntraced();
                    return trace.Id == 0 ? operation : ObserveTracedCacheOperationAsync(operation, trace, "缓存清理完成");
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "缓存清理异常", error);
                throw;
            }
        }

        public ValueTask InvalidateCacheAsync()
        {
            AssertThread();
            var trace = BeginOperationTrace(string.Empty, "InvalidateCacheAsync");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = InvalidateCacheAsyncUntraced();
                    return trace.Id == 0 ? operation : ObserveTracedCacheOperationAsync(operation, trace, "缓存代际失效并清理完成");
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "缓存失效异常", error);
                throw;
            }
        }

        public ValueTask ClearInactiveContentAsync()
        {
            AssertThread();
            var trace = BeginOperationTrace(string.Empty, "ClearInactiveContentAsync");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = ClearInactiveContentAsyncUntraced();
                    return trace.Id == 0 ? operation : ObserveTracedCacheOperationAsync(operation, trace, "闲置内容清理完成");
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "闲置内容清理异常", error);
                throw;
            }
        }

        private async ValueTask ObserveTracedCacheOperationAsync(ValueTask operation,
            NavigationTraceOperation trace, string outcome)
        {
            try
            {
                await operation;
                AssertThread();
                FinishOperationTrace(trace, default, outcome, null);
            }
            catch (Exception error)
            {
                if (Thread.CurrentThread.ManagedThreadId == thread)
                {
                    FinishOperationTrace(trace, default, "清理异常", error);
                }
                else
                {
                    trace.MarkFinished();
                }
                throw;
            }
        }
    }
}
