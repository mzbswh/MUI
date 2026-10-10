using System.Threading.Tasks;

namespace MUI.Navigation
{
    public enum ReplaceStatus
    {
        Rejected,
        CancelledBeforeCommit,
        PreparationFailed,
        Committed
    }

    public enum ReplaceRejection
    {
        None,
        Busy,
        Reentrant,
        SourceUnavailable,
        InstanceLimit,
        CloseDenied,
        ConfirmationUnavailable,
        Superseded,
        CleanupCapacity,
        ConflictingData,
        DependencyCycle,
        DependencyLimit,
        DependencyOrderConflict,
        CloseDecisionTimedOut,
        DependencyMissing,
        RenderOrderCapacity
    }

    /// <summary>Committed 不可逆，即使目标激活或源清理失败也不会回滚提交。</summary>
    public readonly struct ReplaceOutcome<TResult>
    {
        private readonly Task<CloseOutcome> sourceCleanup;

        internal ReplaceOutcome(ReplaceStatus status,
                    OpenOutcome<TResult> destination = default,
                    ReplaceRejection rejection = ReplaceRejection.None,
                    Task<CloseOutcome> sourceCleanup = null)
        {
            Status = status;
            Destination = destination;
            Rejection = rejection;
            this.sourceCleanup = sourceCleanup;
        }

        public ReplaceStatus Status
        {
            get;
        }

        public ReplaceRejection Rejection
        {
            get;
        }

        public OpenOutcome<TResult> Destination
        {
            get;
        }

        /// <summary>仅提交后可用；等待源页面清理结果，源清理不会延迟目标就绪。</summary>
        public Task<CloseOutcome> SourceCleanup => sourceCleanup;

        public bool IsCommitted => Status == ReplaceStatus.Committed;
    }
}
