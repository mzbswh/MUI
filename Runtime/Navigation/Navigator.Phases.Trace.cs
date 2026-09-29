using System;
using System.Diagnostics;
using System.Threading;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private OperationPhaseTraceScope BeginOperationPhaseTrace(ViewInstance instance, NavigationOperationStage stage,
            NavigationTraceOperation operation = default)
        {
            if (!traceRecording || (operation.Id != 0 && !ReferenceEquals(operation.Session, traceSession)))
            {
                return null;
            }

            return new OperationPhaseTraceScope(this, instance.Handle, instance.Route.Key, stage, operation);
        }

        private Func<IViewResourceReleaseTraceScope> CreateResourceReleaseTrace(ViewHandle handle, string key,
            NavigationTraceOperation operation)
        {
            if (!traceRecording || (operation.Id != 0 && !ReferenceEquals(operation.Session, traceSession)))
            {
                return null;
            }

            return CreateResourceReleaseTraceFactory(handle, NavigationTraceEntry.Limit(key), operation, traceSession);
        }

        private Func<IViewResourceReleaseTraceScope> CreateResourceReleaseTraceFactory(ViewHandle handle, string routeKey,
            NavigationTraceOperation operation, object session)
        {
            // 清理可能晚于请求返回；保留发起编号，但不把旧会话的后续阶段写入新会话。
            return () => traceRecording && ReferenceEquals(session, traceSession)
                ? new OperationPhaseTraceScope(this, handle, routeKey, NavigationOperationStage.ViewResourceRelease, operation)
                : null;
        }

        private sealed class OperationPhaseTraceScope : IViewResourceReleaseTraceScope
        {
            private Navigator owner;
            private readonly object session;
            private readonly long operationId;
            private readonly string operationName;
            private readonly ViewHandle handle;
            private readonly string key;
            private readonly NavigationOperationStage stage;
            private readonly long startedAt;
            private bool completed;
            private Exception error;

            internal OperationPhaseTraceScope(Navigator owner, ViewHandle handle, string key,
                NavigationOperationStage stage, NavigationTraceOperation operation)
            {
                this.owner = owner;
                session = owner.traceSession;
                operationId = operation.Id;
                operationName = operation.Id == 0 ? "Internal" : operation.Name;
                this.handle = handle;
                this.key = NavigationTraceEntry.Limit(key);
                this.stage = stage;
                startedAt = Stopwatch.GetTimestamp();
                owner.AppendTrace(new NavigationTraceEntry(owner.host, operationId, operationName,
                    handle, key, stage, startedAt, false, false, null));
            }

            public void Complete() => completed = true;

            public void Fail(Exception failure) => error = failure;

            public void Dispose()
            {
                var navigator = owner;
                owner = null;
                if (navigator == null || Thread.CurrentThread.ManagedThreadId != navigator.thread ||
                    !navigator.traceRecording || !ReferenceEquals(session, navigator.traceSession))
                {
                    return;
                }

                navigator.AppendTrace(new NavigationTraceEntry(navigator.host, operationId, operationName,
                    handle, key, stage, startedAt, true, completed, error));
            }
        }
    }
}
