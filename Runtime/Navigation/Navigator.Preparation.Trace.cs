using System;
using System.Diagnostics;
using System.Threading;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private static void AssignPreparationTrace(ViewInstance candidate, NavigationTraceOperation operation)
        {
            candidate.PreparationOperationId = operation.Id;
            candidate.PreparationTraceSession = operation.Session;
        }

        internal bool IsPreparationTraceEnabled(ViewInstance candidate) => traceRecording &&
            (candidate.PreparationOperationId == 0 || ReferenceEquals(candidate.PreparationTraceSession, traceSession));

        private PreparationTraceScope BeginPreparationTrace(ViewInstance candidate, NavigationPreparationStage stage)
        {
            return new PreparationTraceScope(this, candidate, stage);
        }

        internal IViewPreparationTraceScope BeginPreparationStepTrace(ViewInstance candidate, ViewPreparationStep step)
        {
            return BeginPreparationTrace(candidate, step switch
            {
                ViewPreparationStep.PresenterCreate => NavigationPreparationStage.PresenterCreate,
                ViewPreparationStep.Binding => NavigationPreparationStage.Binding,
                ViewPreparationStep.PresenterOpen => NavigationPreparationStage.PresenterOpen,
                ViewPreparationStep.PresenterOpenAsync => NavigationPreparationStage.PresenterOpenAsync,
                _ => throw new ArgumentOutOfRangeException(nameof(step))
            });
        }

        /// <summary>提供异常阶段上下文，时间线仍按需记录；不持有候选或资源。</summary>
        private sealed class PreparationTraceScope : IViewPreparationTraceScope
        {
            private Navigator owner;
            private readonly object session;
            private readonly long operationId;
            private readonly ViewHandle handle;
            private readonly string key;
            private readonly NavigationPreparationStage stage;
            private readonly long startedAt;
            private bool completed;
            private readonly IDisposable diagnostic;
            private readonly bool recording;

            internal PreparationTraceScope(Navigator owner, ViewInstance candidate, NavigationPreparationStage stage)
            {
                this.owner = owner;
                session = owner.traceSession;
                operationId = candidate.PreparationOperationId;
                handle = candidate.Handle;
                key = candidate.Route.Key;
                this.stage = stage;
                diagnostic = UIErrors.BeginContext(new UIErrorContext(owner.host, handle.Id, key,
                    null, stage.ToString()));
                recording = owner.IsPreparationTraceEnabled(candidate);
                startedAt = Stopwatch.GetTimestamp();
                if (recording)
                {
                    owner.AppendTrace(new NavigationTraceEntry(owner.host, operationId, handle, key,
                        stage, startedAt, false, false));
                }
            }

            public void Complete() => completed = true;

            public void Fail(Exception error) => UIErrors.AttachContext(error, UIErrors.CurrentContext);

            public void Dispose()
            {
                var navigator = owner;
                owner = null;
                diagnostic.Dispose();
                if (navigator == null || !recording || Thread.CurrentThread.ManagedThreadId != navigator.thread ||
                    !navigator.traceRecording || !ReferenceEquals(session, navigator.traceSession))
                {
                    return;
                }
                navigator.AppendTrace(new NavigationTraceEntry(navigator.host, operationId, handle, key,
                    stage, startedAt, true, completed));
            }
        }
    }
}
