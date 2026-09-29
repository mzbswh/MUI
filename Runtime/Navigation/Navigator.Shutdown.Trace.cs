using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>同步退出导航器并记录结果；不表示外层 UIHost 拥有的资源提供方已经销毁。</summary>
        public void Shutdown()
        {
            RequireSynchronousNavigation();
            var trace = BeginOperationTrace(string.Empty, "Shutdown");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    ShutdownUntraced();
                    FinishOperationTrace(trace, default, "导航器退出完成", null);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "退出异常", error);
                throw;
            }
        }

        public ValueTask ShutdownAsync()
        {
            AssertThread();
            if (Mode == LifetimeMode.Synchronous)
            {
                Shutdown();
                return default;
            }
            var trace = BeginOperationTrace(string.Empty, "ShutdownAsync");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = ShutdownAsyncUntraced();
                    return trace.Id == 0 ? operation : ObserveTracedShutdownAsync(operation, trace);
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "退出异常", error);
                throw;
            }
        }

        /// <summary>外层宿主的退出路径也记录导航清理，多个等待者共用原退出任务。</summary>
        internal Task RequestShutdownForHost()
        {
            AssertThread();
            if (Mode == LifetimeMode.Synchronous)
            {
                Shutdown();
                return Task.CompletedTask;
            }
            var trace = BeginOperationTrace(string.Empty, "ShutdownForHost");
            try
            {
                using (EnterOperationTrace(trace))
                {
                    var operation = RequestShutdownForHostUntraced();
                    return trace.Id == 0 ? operation : ObserveTracedShutdownAsync(new ValueTask(operation), trace).AsTask();
                }
            }
            catch (Exception error)
            {
                FinishOperationTrace(trace, default, "退出异常", error);
                throw;
            }
        }

        private async ValueTask ObserveTracedShutdownAsync(ValueTask operation, NavigationTraceOperation trace)
        {
            try
            {
                await operation;
                AssertThread();
                FinishOperationTrace(trace, default, "导航器退出完成", null);
            }
            catch (Exception error)
            {
                if (Thread.CurrentThread.ManagedThreadId == thread)
                {
                    FinishOperationTrace(trace, default, "退出异常", error);
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
