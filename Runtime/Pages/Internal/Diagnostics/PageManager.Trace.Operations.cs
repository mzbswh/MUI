using MUI.Pages.Diagnostics;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        private AsyncLocal<PageTraceOperation> activeTraceOperation;

        private PageTraceOperation CurrentTraceOperation
        {
            get
            {
                var operation = activeTraceOperation == null ? default : activeTraceOperation.Value;
                return traceRecording && operation.Id != 0 &&
                    ReferenceEquals(operation.Session, traceSession) && !operation.IsFinished
                    ? operation : default;
            }
        }

        private TraceOperationScope EnterOperationTrace(PageTraceOperation operation,
            [CallerMemberName] string operationName = null)
        {
            var diagnostic = UIErrors.BeginContext(new UIErrorContext(host, operation.Source.Id,
                operation.Key, operation.Name ?? operationName, "Admission"));
            if (operation.Id == 0)
            {
                return new TraceOperationScope(null, operation, diagnostic);
            }

            if (activeTraceOperation == null)
            {
                activeTraceOperation = new AsyncLocal<PageTraceOperation>();
            }
            return new TraceOperationScope(activeTraceOperation, operation, diagnostic);
        }

        /// <summary>仅追踪启用时观察原操作一次；完成回调仅限框架元数据适配，不捕获业务参数。</summary>
        private async ValueTask<T> ObserveTracedOperationAsync<T>(ValueTask<T> operation,
            PageTraceOperation trace, Action<PageTraceOperation, T> finish)
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

        private PageTraceOperation BeginOperationTrace(string key, string name, PageHandle source = default)
        {
            if (!traceRecording || nextTracedOperationId == long.MaxValue)
            {
                return PageTraceOperation.ForDiagnostics(name, key, source);
            }
            var operation = new PageTraceOperation(traceSession, ++nextTracedOperationId,
                name, PageTraceEntry.Limit(key), Stopwatch.GetTimestamp(), source);
            AppendTrace(new PageTraceEntry(host, operation.Id, name, operation.Key,
                operation.Timestamp, false, source, string.Empty, null));
            return operation;
        }

        private void FinishOperationTrace(PageTraceOperation operation, PageHandle handle, string outcome, Exception error, PageHandle related = default)
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
            AppendTrace(new PageTraceEntry(host, operation.Id, operation.Name, operation.Key,
                operation.Timestamp, true, handle, outcome, error, related.IsValid ? related : operation.Source != handle ? operation.Source : default));
        }

        /// <summary>只保存本次追踪的标识和字符串，不持有页面定义、参数或项目对象。</summary>
        private readonly struct PageTraceOperation
        {
            private readonly TraceOperationCompletion completion;

            internal PageTraceOperation(object session, long id, string name, string key, long timestamp, PageHandle source)
                : this(session, id, name, key, timestamp, source, new TraceOperationCompletion())
            {
            }

            private PageTraceOperation(object session, long id, string name, string key, long timestamp,
                PageHandle source, TraceOperationCompletion completion)
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

            internal PageHandle Source
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

            internal static PageTraceOperation ForDiagnostics(string name, string key, PageHandle source) =>
                new PageTraceOperation(null, 0, name, key, 0, source, null);

            internal void MarkFinished() => completion.Finished = true;

            internal PageTraceOperation WithTarget(string key, PageHandle source) =>
                new PageTraceOperation(Session, Id, Name, key, Timestamp, source, completion);
        }

        private sealed class TraceOperationCompletion
        {
            internal volatile bool Finished;
        }

        private readonly struct TraceOperationScope : IDisposable
        {
            private readonly AsyncLocal<PageTraceOperation> context;
            private readonly PageTraceOperation previous;
            private readonly IDisposable diagnostic;

            internal TraceOperationScope(AsyncLocal<PageTraceOperation> context,
                PageTraceOperation operation, IDisposable diagnostic)
            {
                this.context = context;
                this.diagnostic = diagnostic;
                previous = context == null ? default : context.Value;
                if (context != null)
                {
                    context.Value = operation;
                }
            }

            public void Dispose()
            {
                if (context != null)
                {
                    context.Value = previous;
                }

                diagnostic?.Dispose();
            }
        }
    }
}
