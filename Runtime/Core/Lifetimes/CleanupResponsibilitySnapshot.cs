using System;

namespace MUI
{
    public enum CleanupResponsibilityState
    {
        Retained,
        Pending,
        Failed,
        Completed
    }

    /// <summary>当前尝试的诊断，不代表首次释放任务的历史结果发生变化。</summary>
    public readonly struct CleanupResponsibilitySnapshot
    {
        internal CleanupResponsibilitySnapshot(Guid id, string owner, CleanupResponsibilityState state,
            Exception failure, UIErrorContext context, long attempts, bool canRetry)
        {
            Id = id;
            Owner = owner;
            State = state;
            Failure = failure;
            Context = context;
            Attempts = attempts;
            CanRetry = canRetry;
        }

        public Guid Id
        {
            get;
        }

        public string Owner
        {
            get;
        }

        public CleanupResponsibilityState State
        {
            get;
        }

        public Exception Failure
        {
            get;
        }

        public UIErrorContext Context
        {
            get;
        }

        public long Attempts
        {
            get;
        }

        public bool CanRetry
        {
            get;
        }

        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Failure);
    }
}
