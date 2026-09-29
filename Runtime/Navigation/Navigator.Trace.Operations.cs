using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private AsyncLocal<NavigationTraceOperation> activeTraceOperation;

        private NavigationTraceOperation CurrentTraceOperation
        {
            get
            {
                var operation = activeTraceOperation == null ? default : activeTraceOperation.Value;
                return traceRecording && operation.Id != 0 &&
                    ReferenceEquals(operation.Session, traceSession) && !operation.IsFinished
                    ? operation : default;
            }
        }

        private TraceOperationScope EnterOperationTrace(NavigationTraceOperation operation)
        {
            if (operation.Id == 0)
            {
                return default;
            }

            if (activeTraceOperation == null)
            {
                activeTraceOperation = new AsyncLocal<NavigationTraceOperation>();
            }
            return new TraceOperationScope(activeTraceOperation, operation);
        }

        /// <summary>仅追踪启用时观察原操作一次；完成回调仅限框架元数据适配，不捕获业务参数。</summary>
        private async ValueTask<T> ObserveTracedOperationAsync<T>(ValueTask<T> operation,
            NavigationTraceOperation trace, Action<NavigationTraceOperation, T> finish)
        {
            try
            {
                var result = await operation;
                AssertThread();
                finish(trace, result);
                return result;
            }
            catch (Exception error)
            {
                if (Thread.CurrentThread.ManagedThreadId == thread)
                {
                    FinishOperationTrace(trace, trace.Source, "抛出异常", error);
                }
                else
                {
                    trace.MarkFinished();
                }
                throw;
            }
        }

        private NavigationTraceOperation BeginOperationTrace(string key, string name, ViewHandle source = default)
        {
            if (!traceRecording || nextTracedOperationId == long.MaxValue)
            {
                return default;
            }
            var operation = new NavigationTraceOperation(traceSession, ++nextTracedOperationId,
                name, NavigationTraceEntry.Limit(key), Stopwatch.GetTimestamp(), source);
            AppendTrace(new NavigationTraceEntry(host, operation.Id, name, operation.Key,
                operation.Timestamp, false, source, string.Empty, null));
            return operation;
        }

        private void FinishOperationTrace(NavigationTraceOperation operation, ViewHandle handle, string outcome, Exception error, ViewHandle related = default)
        {
            // 停止或重新开始后，旧在途请求的完成不能污染新一轮时间线。
            if (operation.Id == 0 || operation.IsFinished)
            {
                return;
            }
            operation.MarkFinished();
            if (!traceRecording || !ReferenceEquals(operation.Session, traceSession))
            {
                return;
            }
            AppendTrace(new NavigationTraceEntry(host, operation.Id, operation.Name, operation.Key,
                operation.Timestamp, true, handle, outcome, error, related.IsValid ? related : operation.Source != handle ? operation.Source : default));
        }

        /// <summary>只保存本次追踪的标识和字符串，不持有路由、参数或项目对象。</summary>
        private readonly struct NavigationTraceOperation
        {
            private readonly TraceOperationCompletion completion;

            internal NavigationTraceOperation(object session, long id, string name, string key, long timestamp, ViewHandle source)
                : this(session, id, name, key, timestamp, source, new TraceOperationCompletion())
            {
            }

            private NavigationTraceOperation(object session, long id, string name, string key, long timestamp,
                ViewHandle source, TraceOperationCompletion completion)
            {
                Session = session;
                Id = id;
                Name = name;
                Key = key;
                Timestamp = timestamp;
                Source = source;
                this.completion = completion;
            }

            internal bool IsFinished => completion == null || completion.Finished;

            internal ViewHandle Source
            {
                get;
            }

            internal object Session
            {
                get;
            }

            internal long Id
            {
                get;
            }

            internal string Name
            {
                get;
            }

            internal string Key
            {
                get;
            }

            internal long Timestamp
            {
                get;
            }

            internal void MarkFinished() => completion.Finished = true;

            internal NavigationTraceOperation WithTarget(string key, ViewHandle source) =>
                new NavigationTraceOperation(Session, Id, Name, key, Timestamp, source, completion);
        }

        private sealed class TraceOperationCompletion
        {
            internal volatile bool Finished;
        }

        private readonly struct TraceOperationScope : IDisposable
        {
            private readonly AsyncLocal<NavigationTraceOperation> context;
            private readonly NavigationTraceOperation previous;

            internal TraceOperationScope(AsyncLocal<NavigationTraceOperation> context,
                NavigationTraceOperation operation)
            {
                this.context = context;
                previous = context.Value;
                context.Value = operation;
            }

            public void Dispose()
            {
                if (context != null)
                {
                    context.Value = previous;
                }
            }
        }
    }
}
