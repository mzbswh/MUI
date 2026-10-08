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
            return new OperationPhaseTraceScope(this, instance.Handle, instance.Route.Key, stage, operation);
        }

        private Func<IViewResourceReleaseTraceScope> CreateResourceReleaseTrace(ViewHandle handle, string key,
            NavigationTraceOperation operation)
        {
            return CreateResourceReleaseTraceFactory(handle, NavigationTraceEntry.Limit(key), operation, traceSession);
        }

        private Func<IViewResourceReleaseTraceScope> CreateResourceReleaseTraceFactory(ViewHandle handle, string routeKey,
            NavigationTraceOperation operation, object session)
        {
            // 清理可能晚于请求返回；保留发起编号，但不把旧会话的后续阶段写入新会话。
            return () => new OperationPhaseTraceScope(this, handle, routeKey,
                NavigationOperationStage.ViewResourceRelease, operation,
                ReferenceEquals(session, traceSession));
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
            private readonly UIErrorContext errorContext;
            private readonly IDisposable diagnostic;
            private readonly bool recording;

            internal OperationPhaseTraceScope(Navigator owner, ViewHandle handle, string key,
                NavigationOperationStage stage, NavigationTraceOperation operation, bool sameSession = true)
            {
                this.owner = owner;
                session = owner.traceSession;
                operationId = operation.Id;
                operationName = operation.Id == 0 ? "Internal" : operation.Name;
                this.handle = handle;
                this.key = NavigationTraceEntry.Limit(key);
                this.stage = stage;
                recording = sameSession && owner.traceRecording &&
                    (operation.Id == 0 || ReferenceEquals(operation.Session, owner.traceSession));
                errorContext = new UIErrorContext(owner.host, handle.Id, key,
                    operation.Name ?? UIErrors.CurrentContext.Operation ?? "Internal", stage.ToString());
                // 进入和退出阶段可跨帧保存，不能让它们把上下文留在帧驱动调用链上。
                if (stage == NavigationOperationStage.InstanceCleanup || stage == NavigationOperationStage.ViewResourceRelease)
                {
                    diagnostic = UIErrors.BeginContext(errorContext);
                }
                startedAt = Stopwatch.GetTimestamp();
                if (recording)
                {
                    owner.AppendTrace(new NavigationTraceEntry(owner.host, operationId, operationName,
                        handle, key, stage, startedAt, false, false, null));
                }
            }

            public void Complete() => completed = true;

            public void Fail(Exception failure)
            {
                error = failure;
                UIErrors.AttachContext(failure, errorContext);
            }

            public void Dispose()
            {
                var navigator = owner;
                owner = null;
                diagnostic?.Dispose();
                if (navigator == null || !recording || Thread.CurrentThread.ManagedThreadId != navigator.thread ||
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
