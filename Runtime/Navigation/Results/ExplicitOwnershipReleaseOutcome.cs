using System;

namespace MUI.Navigation
{
    public enum ExplicitOwnershipReleaseStatus
    {
        Rejected,
        Released,
        AlreadyReleased,
        Pending
    }

    /// <summary>显式持有关系的撤销结果；Released 不等于界面已经关闭。</summary>
    public readonly struct ExplicitOwnershipReleaseOutcome
    {
        internal ExplicitOwnershipReleaseOutcome(ExplicitOwnershipReleaseStatus status,
            CloseOutcome? closeOutcome = null, Exception error = null)
        {
            Status = status;
            CloseOutcome = closeOutcome;
            Error = error ?? closeOutcome?.Error;
        }

        public ExplicitOwnershipReleaseStatus Status
        {
            get;
        }

        /// <summary>仅需要关闭实例时提供；仍被父页面持有时为 null。</summary>
        public CloseOutcome? CloseOutcome
        {
            get;
        }

        /// <summary>关闭或提交后表现更新错误；存在错误不撤销已提交的所有权变化。</summary>
        public Exception Error
        {
            get;
        }

        /// <summary>与错误出口共享的诊断标识；无异常时为 Guid.Empty。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(Error);

        public bool IsReleased => Status == ExplicitOwnershipReleaseStatus.Released ||
            Status == ExplicitOwnershipReleaseStatus.AlreadyReleased;
    }
}
