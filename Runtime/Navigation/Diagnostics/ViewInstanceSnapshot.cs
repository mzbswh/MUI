using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    /// <summary>快照采集时正在执行或等待收敛的导航操作，可同时存在多个标记。</summary>
    [Flags]
    public enum ViewOperationFlags
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

    /// <summary>
    /// 单个导航实例的只读快照，不持有 View、ViewModel、路由工厂、参数或任务。
    /// 可见与交互字段仅表示导航门控，不等同于渲染器最终显示或 Element 可命中。
    /// </summary>
    public sealed class ViewInstanceSnapshot
    {
        internal ViewInstanceSnapshot(ViewInstance instance, int presentationIndex, int historyIndex,
                    bool explicitOwner, int ownerCount, ViewHandle[] owners, ViewHandle[] dependencies,
                    ViewOperationFlags operations, int executingCommands)
        {
            Handle = instance.Handle;
            RouteKey = instance.Route.Key;
            ResourceKey = instance.Route.Resource.Key;
            ResourceVersion = instance.Route.Resource.Version;
            State = instance.State;
            Layer = instance.Route.Policy.Layer;
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
                instance.HasCloseStarted || (operations & ViewOperationFlags.DependencyExit) != 0
                    ? CleanupStatus.Pending : CleanupStatus.NotRequired;
        }

        public ViewHandle Handle
        {
            get;
        }

        public string RouteKey
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

        /// <summary>实例置前顺序号；真实导航显示位置读取 PresentationIndex。</summary>
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
        public ViewHandle HiddenBy
        {
            get;
        }

        /// <summary>最近一次表现重算中，最近的上层输入覆盖来源，不包含渲染器局部输入锁。</summary>
        public ViewHandle BlockedBy
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

        public ViewOperationFlags Operations
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

        public IReadOnlyList<ViewHandle> Owners
        {
            get;
        }

        public bool OwnersTruncated => Owners.Count < OwnerCount;

        public IReadOnlyList<ViewHandle> Dependencies
        {
            get;
        }

        public bool HasReadiness
        {
            get;
        }

        /// <summary>仅 HasReadiness 为 true 时有意义；不触发任务或等待首次就绪。</summary>
        public ViewReadinessStatus ReadinessStatus
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
