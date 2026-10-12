using MUI.Pages.Diagnostics;
using System;
using System.Diagnostics;
using System.Threading;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        private static void AssignPreparationTrace(PageInstance candidate, PageTraceOperation operation)
        {
            candidate.PreparationOperationId = operation.Id;
            candidate.PreparationTraceSession = operation.Session;
        }

        internal bool IsPreparationTraceEnabled(PageInstance candidate) => traceBuffer.IsRecording &&
            (candidate.PreparationOperationId == 0 || ReferenceEquals(candidate.PreparationTraceSession, traceBuffer.Session));

        private PreparationTraceScope BeginPreparationTrace(PageInstance candidate, PagePreparationStage stage)
        {
            return new PreparationTraceScope(this, candidate, stage);
        }

        internal IViewPreparationTraceScope BeginPreparationStepTrace(PageInstance candidate, ViewPreparationStep step)
        {
            return BeginPreparationTrace(candidate, step switch
            {
                ViewPreparationStep.PresenterCreate => PagePreparationStage.PresenterCreate,
                ViewPreparationStep.Binding => PagePreparationStage.Binding,
                ViewPreparationStep.PresenterPrepareOpen => PagePreparationStage.PresenterPrepareOpen,
                ViewPreparationStep.PresenterPrepareOpenAsync => PagePreparationStage.PresenterPrepareOpenAsync,
                _ => throw new ArgumentOutOfRangeException(nameof(step))
            });
        }

        /// <summary>提供异常阶段上下文，时间线仍按需记录；不持有候选或资源。</summary>
        private sealed class PreparationTraceScope : IViewPreparationTraceScope
        {
            private PageManager owner;
            private readonly object session;
            private readonly long operationId;
            private readonly PageHandle handle;
            private readonly string key;
            private readonly PagePreparationStage stage;
            private readonly long startedAt;
            private bool completed;
            private readonly IDisposable diagnostic;
            private readonly bool recording;

            internal PreparationTraceScope(PageManager owner, PageInstance candidate, PagePreparationStage stage)
            {
                this.owner = owner;
                session = owner.traceBuffer.Session;
                operationId = candidate.PreparationOperationId;
                handle = candidate.Handle;
                key = candidate.Definition.Key;
                this.stage = stage;
                diagnostic = UIErrors.BeginContext(new UIErrorContext(owner.host, handle.Id, key,
                    null, stage.ToString()));
                recording = owner.IsPreparationTraceEnabled(candidate);
                startedAt = Stopwatch.GetTimestamp();
                if (recording)
                {
                    owner.AppendTrace(new PageTraceEntry(owner.host, operationId, handle, key,
                        stage, startedAt, false, false));
                }
            }

            public void Complete() => completed = true;

            public void Fail(Exception error) => UIErrors.AttachContext(error, UIErrors.CurrentContext);

            public void Dispose()
            {
                var pages = owner;
                owner = null;
                diagnostic.Dispose();
                if (pages == null || !recording || Thread.CurrentThread.ManagedThreadId != pages.thread ||
                    !pages.traceBuffer.IsRecording || !ReferenceEquals(session, pages.traceBuffer.Session))
                {
                    return;
                }
                pages.AppendTrace(new PageTraceEntry(pages.host, operationId, handle, key,
                    stage, startedAt, true, completed));
            }
        }
    }
}
