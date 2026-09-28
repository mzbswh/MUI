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
            if (!IsPreparationTraceEnabled(candidate))
            {
                return null;
            }
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

        /// <summary>仅启用追踪时分配；不创建任务、不持有候选或资源，不通过异常消息推断结果。</summary>
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

            internal PreparationTraceScope(Navigator owner, ViewInstance candidate, NavigationPreparationStage stage)
            {
                this.owner = owner;
                session = owner.traceSession;
                operationId = candidate.PreparationOperationId;
                handle = candidate.Handle;
                key = candidate.Route.Key;
                this.stage = stage;
                startedAt = Stopwatch.GetTimestamp();
                owner.AppendTrace(new NavigationTraceEntry(owner.host, operationId, handle, key,
                    stage, startedAt, false, false));
            }

            public void Complete() => completed = true;

            public void Dispose()
            {
                var navigator = owner;
                owner = null;
                if (navigator == null || Thread.CurrentThread.ManagedThreadId != navigator.thread ||
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
