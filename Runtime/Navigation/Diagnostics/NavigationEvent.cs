using System;

namespace MUI.Navigation
{
    public enum NavigationEventKind
    {
        OpenCommitted,
        CloseCommitted,
        Closed,
        ArgsUpdateFinished,
        ViewModelRebindFinished,
        CloseCleanupPending,
        ExplicitOwnershipReleased,
        DependencyDegraded
    }

    /// <summary>不包含 View 或 ViewModel 引用的不可变生命周期快照。</summary>
    public readonly struct NavigationEvent
    {
        internal NavigationEvent(NavigationEventKind kind,
                    Route route,
                    ViewHandle handle,
                    long sequence,
                    long commitVersion,
                    DismissReason? reason,
                    CloseOutcome? closeOutcome,
                    ArgsUpdateOutcome? argsUpdateOutcome = null,
                    RebindOutcome? rebindOutcome = null,
                    DependencyFailure? dependencyFailure = null)
        {
            Kind = kind;
            Route = route;
            Handle = handle;
            Sequence = sequence;
            CommitVersion = commitVersion;
            Reason = reason;
            CloseOutcome = closeOutcome;
            ArgsUpdateOutcome = argsUpdateOutcome;
            RebindOutcome = rebindOutcome;
            DependencyFailure = dependencyFailure;
        }

        /// <summary>与操作结果及追踪共享的异常标识，不触发错误报告。</summary>
        public Guid DiagnosticId => UIErrors.GetDiagnosticId(CloseOutcome?.Error ?? ArgsUpdateOutcome?.Error ??
            RebindOutcome?.Error ?? DependencyFailure?.Error);

        /// <summary>仅可选依赖降级事件携带；实例退出后也可读取此快照。</summary>
        public DependencyFailure? DependencyFailure
        {
            get;
        }

        public NavigationEventKind Kind
        {
            get;
        }

        public Route Route
        {
            get;
        }

        public ViewHandle Handle
        {
            get;
        }

        public long Sequence
        {
            get;
        }

        /// <summary>事件入队时实例的最新提交版本；更新完成事件可能已包含随后发生的关闭版本。</summary>
        public long CommitVersion
        {
            get;
        }

        public DismissReason? Reason
        {
            get;
        }

        public CloseOutcome? CloseOutcome
        {
            get;
        }

        /// <summary>仅参数更新完成事件携带；包含提交、故障及清理结果，不含参数值。</summary>
        public ArgsUpdateOutcome? ArgsUpdateOutcome
        {
            get;
        }

        /// <summary>仅模型换绑完成事件携带；不持有旧模型或新模型。</summary>
        public RebindOutcome? RebindOutcome
        {
            get;
        }
    }
}
