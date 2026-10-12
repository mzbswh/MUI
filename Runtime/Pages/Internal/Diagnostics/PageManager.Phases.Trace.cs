using MUI.Pages.Diagnostics;
using System;
using System.Diagnostics;
using System.Threading;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        private OperationPhaseTraceScope BeginOperationPhaseTrace(PageInstance instance, PageOperationStage stage,
            PageTraceOperation operation = default)
        {
            return new OperationPhaseTraceScope(this, instance.Handle, instance.Definition.Key, stage, operation);
        }

        private Func<IViewResourceReleaseTraceScope> CreateResourceReleaseTrace(PageHandle handle, string key,
            PageTraceOperation operation)
        {
            return CreateResourceReleaseTraceFactory(handle, PageTraceEntry.Limit(key), operation, traceBuffer.Session);
        }

        private Func<IViewResourceReleaseTraceScope> CreateResourceReleaseTraceFactory(PageHandle handle, string pageKey,
            PageTraceOperation operation, object session)
        {
            // 清理可能晚于请求返回；保留发起编号，但不把旧会话的后续阶段写入新会话。
            return () => new OperationPhaseTraceScope(this, handle, pageKey,
                PageOperationStage.ViewResourceRelease, operation,
                ReferenceEquals(session, traceBuffer.Session));
        }

        private sealed class OperationPhaseTraceScope : IViewResourceReleaseTraceScope
        {
            private PageManager owner;
            private readonly object session;
            private readonly long operationId;
            private readonly string operationName;
            private readonly PageHandle handle;
            private readonly string key;
            private readonly PageOperationStage stage;
            private readonly long startedAt;
            private bool completed;
            private Exception error;
            private readonly UIErrorContext errorContext;
            private readonly IDisposable diagnostic;
            private readonly bool recording;

            internal OperationPhaseTraceScope(PageManager owner, PageHandle handle, string key,
                PageOperationStage stage, PageTraceOperation operation, bool sameSession = true)
            {
                this.owner = owner;
                session = owner.traceBuffer.Session;
                operationId = operation.Id;
                operationName = operation.Id == 0 ? "Internal" : operation.Name;
                this.handle = handle;
                this.key = PageTraceEntry.Limit(key);
                this.stage = stage;
                recording = sameSession && owner.traceBuffer.IsRecording &&
                    (operation.Id == 0 || ReferenceEquals(operation.Session, owner.traceBuffer.Session));
                errorContext = new UIErrorContext(owner.host, handle.Id, key,
                    operation.Name ?? UIErrors.CurrentContext.Operation ?? "Internal", stage.ToString());
                // 进入和退出阶段可跨帧保存，不能让它们把上下文留在帧驱动调用链上。
                if (stage == PageOperationStage.InstanceCleanup || stage == PageOperationStage.ViewResourceRelease)
                {
                    diagnostic = UIErrors.BeginContext(errorContext);
                }
                startedAt = Stopwatch.GetTimestamp();
                if (recording)
                {
                    owner.AppendTrace(new PageTraceEntry(owner.host, operationId, operationName,
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
                var pages = owner;
                owner = null;
                diagnostic?.Dispose();
                if (pages == null || !recording || Thread.CurrentThread.ManagedThreadId != pages.thread ||
                    !pages.traceBuffer.IsRecording || !ReferenceEquals(session, pages.traceBuffer.Session))
                {
                    return;
                }

                pages.AppendTrace(new PageTraceEntry(pages.host, operationId, operationName,
                    handle, key, stage, startedAt, true, completed, error));
            }
        }
    }
}
