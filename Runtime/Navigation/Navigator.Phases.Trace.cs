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

            return new OperationPhaseTraceScope(this, instance, stage, operation);
        }

        private sealed class OperationPhaseTraceScope : IDisposable
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

            internal OperationPhaseTraceScope(Navigator owner, ViewInstance instance,
                NavigationOperationStage stage, NavigationTraceOperation operation)
            {
                this.owner = owner;
                session = owner.traceSession;
                operationId = operation.Id;
                operationName = operation.Id == 0 ? "Internal" : operation.Name;
                handle = instance.Handle;
                key = instance.Route.Key;
                this.stage = stage;
                startedAt = Stopwatch.GetTimestamp();
                owner.AppendTrace(new NavigationTraceEntry(owner.host, operationId, operationName,
                    handle, key, stage, startedAt, false, false, null));
            }

            internal void Complete() => completed = true;

            internal void Fail(Exception failure) => error = failure;

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
