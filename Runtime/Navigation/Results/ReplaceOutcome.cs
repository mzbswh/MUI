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
        RequiresAsync,
        RequiresPreload,
        SyncCreationUnsupported,
        ConflictingData,
        DependencyCycle,
        DependencyLimit,
        DependencyOrderConflict,
        CloseDecisionTimedOut
    }

    /// <summary>Committed 不可逆，即使目标激活或源清理失败也不会回滚提交。</summary>
    public readonly struct ReplaceOutcome<TResult>
    {
        private readonly Task<CloseOutcome> sourceCleanup;
        private readonly ViewCompletion<CloseOutcome> synchronousSourceCleanup;

        internal ReplaceOutcome(ReplaceStatus status,
                    OpenOutcome<TResult> destination = default,
                    ReplaceRejection rejection = ReplaceRejection.None,
                    Task<CloseOutcome> sourceCleanup = null,
                    CloseOutcome? sourceClose = null,
                    ViewCompletion<CloseOutcome> synchronousSourceCleanup = null)
        {
            Status = status;
            Destination = destination;
            Rejection = rejection;
            this.sourceCleanup = sourceCleanup;
            this.synchronousSourceCleanup = synchronousSourceCleanup;
            SourceClose = sourceClose;
        }

        internal Task<CloseOutcome> ExistingSourceCleanup => sourceCleanup;

        internal ViewCompletion<CloseOutcome> SynchronousSourceCleanup => synchronousSourceCleanup;

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

        /// <summary>仅提交后可用；同步结果仅在显式读取本属性时创建兼容任务。源清理不会延迟目标就绪。</summary>
        public Task<CloseOutcome> SourceCleanup => sourceCleanup ??
            (synchronousSourceCleanup == null ? null : synchronousSourceCleanup.Task);

        /// <summary>同步替换提交后的源清理结果，无需读取任务。</summary>
        public CloseOutcome? SourceClose
        {
            get;
        }

        public bool IsCommitted => Status == ReplaceStatus.Committed;
    }
}
