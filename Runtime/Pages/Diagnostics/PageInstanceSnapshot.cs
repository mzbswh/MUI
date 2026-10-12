using System;
using System.Collections.Generic;

namespace MUI.Pages.Diagnostics
{
    /// <summary>快照采集时正在执行或等待收敛的导航操作，可同时存在多个标记。</summary>
    [Flags]
    public enum PageOperationFlags
    {
        None = 0,
        Preparing = 1,
        UpdatingArgs = 2,
        Rebinding = 4,
        ExecutingCommand = 8,
        Entering = 16,
        Exiting = 32,
        CloseRequested = 64,
        Closing = 128,
        /// <summary>已超过关闭预算，资源仍由宿主隔离持有。</summary>
        CleanupTimedOut = 256,
        DependencyExit = 512
    }

    /// <summary>最近一次帧驱动检查的结果；诊断采集不调用渲染器属性。</summary>
    public enum PageTickStatus
    {
        NotEvaluated,
        Eligible,
        Inactive,
        Rebinding,
        Entering,
        Covered,
        Hidden,
        HostShutdown
    }

    /// <summary>依赖在本次父激活中的取得方式；失败和运行中降级另有说明。</summary>
    public enum DependencyResolutionKind
    {
        Pending,
        AutoOpened,
        Reused,
        Skipped,
        Degraded
    }

    public readonly struct DependencyResolutionSnapshot
    {
        internal DependencyResolutionSnapshot(PageDependencyDescriptor descriptor, DependencyResolutionKind kind, string failure)
        {
            PageKey = descriptor.Target.Key;
            IsRequired = descriptor.IsRequired;
            MissingPolicy = descriptor.MissingPolicy;
            Kind = kind;
            Failure = failure;
        }

        public string PageKey
        {
            get;
        }

        public bool IsRequired
        {
            get;
        }

        public DependencyMissingPolicy MissingPolicy
        {
            get;
        }

        public DependencyResolutionKind Kind
        {
            get;
        }

        public string Failure
        {
            get;
        }
    }

    /// <summary>
    /// 单个导航实例的只读快照，不持有 View、ViewModel、页面定义工厂、参数或任务。
    /// 可见与交互字段仅表示导航门控，不等同于渲染器最终显示或 Element 可命中。
    /// </summary>
    public sealed class PageInstanceSnapshot
    {
        internal PageInstanceSnapshot(PageInstance instance, int presentationIndex, int historyIndex,
                    bool explicitOwner, int ownerCount, PageHandle[] owners, PageHandle[] dependencies,
                    PageOperationFlags operations, int executingCommands, int definitionInstanceCount, int? renderOrderStart, int renderOrderSpan)
        {
            RenderOrderStart = renderOrderStart;
            RenderOrderSpan = renderOrderSpan;
            DefinitionInstanceCount = definitionInstanceCount;
            TickStatus = instance.LastTickStatus;
            var resolutions = new List<DependencyResolutionSnapshot>();
            foreach (var descriptor in instance.Definition.DependencyDescriptors)
            {
                var kind = instance.DependencyResolutions.TryGetValue(descriptor.Target, out var resolution)
                    ? resolution : DependencyResolutionKind.Pending;
                string failureText = null;
                if (instance.DependencyFailures != null && instance.DependencyFailures.TryGetValue(descriptor.Target, out var failure))
                {
                    kind = DependencyResolutionKind.Degraded;
                    // Exception.Message 可被项目异常重写；只读采集不执行该回调。
                    failureText = failure.Stage + ": " + (failure.Error is PagePreparationRejectedException rejection
                        ? rejection.Rejection.ToString() : failure.Error == null ? string.Empty : failure.Error.GetType().Name);
                }
                resolutions.Add(new DependencyResolutionSnapshot(descriptor, kind, failureText));
            }
            DependencyResolutions = resolutions.AsReadOnly();
            ParentPage = instance.ParentPage;
            Handle = instance.Handle;
            PageKey = instance.Definition.Key;
            ResourceKey = instance.Definition.Resource.Key;
            ResourceVersion = instance.Definition.Resource.Version;
            State = instance.State;
            Layer = instance.Definition.Policy.Layer;
            Policy = instance.Definition.Policy;
            Order = instance.Order;
            CommitVersion = instance.CommitVersion;
            PresentationIndex = presentationIndex;
            HistoryIndex = historyIndex;
            HasExplicitOwner = explicitOwner;
            OwnerCount = ownerCount;
            Owners = Array.AsReadOnly(owners);
            Dependencies = Array.AsReadOnly(dependencies);
            Operations = operations;
            ExecutingCommandCount = executingCommands;
            HostVisible = instance.HostVisible;
            HostInteractable = instance.HostInteractable;
            Focused = instance.Focused;
            Covered = instance.HiddenBy.IsValid || instance.BlockedBy.IsValid;
            HiddenBy = instance.HiddenBy;
            BlockedBy = instance.BlockedBy;
            ActivationCommitted = instance.ActivationCommitted;
            PreparationComplete = instance.PreparationComplete;
            HasFailure = instance.Failure != null;
            DependencyFailureCount = instance.DependencyFailures == null ? 0 : instance.DependencyFailures.Count;
            HasReadiness = instance.TryGetReadiness(out var readiness);
            ReadinessStatus = readiness.Status;
            IsReadinessDegraded = HasReadiness && readiness.IsDegraded;
            Cleanup = instance.CompletedCloseOutcome.HasValue ? instance.CompletedCloseOutcome.Value.Cleanup :
                instance.HasCloseStarted || (operations & PageOperationFlags.DependencyExit) != 0
                    ? CleanupStatus.Pending : CleanupStatus.NotRequired;
        }

        public PageHandle ParentPage
        {
            get;
        }

        public int? RenderOrderStart
        {
            get;
        }

        public int RenderOrderSpan
        {
            get;
        }

        public int DefinitionInstanceCount
        {
            get;
        }

        public PageTickStatus TickStatus
        {
            get;
        }

        public IReadOnlyList<DependencyResolutionSnapshot> DependencyResolutions
        {
            get;
        }

        public PageHandle Handle
        {
            get;
        }

        public string PageKey
        {
            get;
        }

        public string ResourceKey
        {
            get;
        }

        public string ResourceVersion
        {
            get;
        }

        public ViewState State
        {
            get;
        }

        public int Layer
        {
            get;
        }

        /// <summary>已解析的不可变策略，包含配置来源；不引用配置资产或页面定义工厂。</summary>
        public PagePolicy Policy
        {
            get;
        }

        /// <summary>请求接纳时确定的导航顺序号，显式置前可更新；当前显示位置读取 PresentationIndex。</summary>
        public long Order
        {
            get;
        }

        public long CommitVersion
        {
            get;
        }

        /// <summary>从底到顶的导航显示索引，不在显示集合中时为 -1。</summary>
        public int PresentationIndex
        {
            get;
        }

        /// <summary>在完整历史中的索引，不在历史中时为 -1。</summary>
        public int HistoryIndex
        {
            get;
        }

        public bool HostVisible
        {
            get;
        }

        public bool HostInteractable
        {
            get;
        }

        public bool Focused
        {
            get;
        }

        public bool Covered
        {
            get;
        }

        /// <summary>最近一次表现重算中，最近的上层 Hide 策略来源；无来源时为无效句柄。</summary>
        public PageHandle HiddenBy
        {
            get;
        }

        /// <summary>最近一次表现重算中，最近的上层输入覆盖来源，不包含渲染器局部输入锁。</summary>
        public PageHandle BlockedBy
        {
            get;
        }

        public bool ActivationCommitted
        {
            get;
        }

        public bool PreparationComplete
        {
            get;
        }

        public bool HasFailure
        {
            get;
        }

        public int DependencyFailureCount
        {
            get;
        }

        public PageOperationFlags Operations
        {
            get;
        }

        /// <summary>本绑定会话所有调用链的在途命令数量，包含同步调用与尚未返回的异步命令。</summary>
        public int ExecutingCommandCount
        {
            get;
        }

        public bool HasExplicitOwner
        {
            get;
        }

        public int OwnerCount
        {
            get;
        }

        public IReadOnlyList<PageHandle> Owners
        {
            get;
        }

        public bool OwnersTruncated => Owners.Count < OwnerCount;

        public IReadOnlyList<PageHandle> Dependencies
        {
            get;
        }

        public bool HasReadiness
        {
            get;
        }

        /// <summary>仅 HasReadiness 为 true 时有意义；不触发任务或等待首次就绪。</summary>
        public PageReadinessStatus ReadinessStatus
        {
            get;
        }

        public bool IsReadinessDegraded
        {
            get;
        }

        public CleanupStatus Cleanup
        {
            get;
        }
    }
}
