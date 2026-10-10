using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    public enum ViewCacheDecision
    {
        Retained,
        Reused,
        Disabled,
        Unsupported,
        AbnormalClose,
        CleanupIncomplete,
        ResetFailed,
        CapacityDisabled,
        HostUnavailable,
        Incompatible,
        EstimateRequired,
        BudgetExceeded,
        RetirementPending,
        EvictedForCapacity,
        Expired,
        Destroyed,
        Cleared,
        ReuseFailed
    }

    /// <summary>有界缓存诊断，不保留 View、路由或异常引用。</summary>
    public readonly struct ViewCacheDiagnostic
    {
        internal ViewCacheDiagnostic(string routeKey, ViewCacheDecision decision)
        {
            RouteKey = routeKey;
            Decision = decision;
            TimestampUtc = DateTime.UtcNow;
        }

        public string RouteKey
        {
            get;
        }

        public ViewCacheDecision Decision
        {
            get;
        }

        public DateTime TimestampUtc
        {
            get;
        }
    }

    /// <summary>所属 UI 线程上的导航状态快照；只包含已复制的元数据，不执行项目或渲染器回调。</summary>
    public sealed class NavigationSnapshot
    {
        internal NavigationSnapshot(bool shutdown, long version, bool presentationSettled,
                    ViewHandle focused, int totalInstances, ViewInstanceSnapshot[] instances, int historyCount,
                    ViewHandle[] history, int pendingRequests, int postedRequests, int cachedViews,
                    int retiringCachedViews, int preloadReservations, int pendingCleanup,
                    bool hasCleanupFailure, long droppedEvents, Guid hostId, int unconfirmedCleanupCount,
                    IReadOnlyList<CleanupResponsibilitySnapshot> cleanupResponsibilities, ViewCacheDiagnostic[] cacheDiagnostics, RenderOrderOptions renderOrder, int reservedRenderOrders)
        {
            RenderOrder = renderOrder;
            ReservedRenderOrders = reservedRenderOrders;
            CacheDiagnostics = Array.AsReadOnly(cacheDiagnostics);
            IsShutdown = shutdown;
            CommitVersion = version;
            IsPresentationSettled = presentationSettled;
            Focused = focused;
            TotalInstances = totalInstances;
            Instances = Array.AsReadOnly(instances);
            HistoryCount = historyCount;
            RecentHistory = Array.AsReadOnly(history);
            PendingRequestCount = pendingRequests;
            PostedRequestCount = postedRequests;
            CachedViewCount = cachedViews;
            RetiringCachedViewCount = retiringCachedViews;
            PreloadReservationCount = preloadReservations;
            PendingCleanupCount = pendingCleanup;
            HasCleanupFailure = hasCleanupFailure;
            HostId = hostId;
            UnconfirmedCleanupCount = unconfirmedCleanupCount;
            CleanupResponsibilities = cleanupResponsibilities;
            DroppedLifecycleEventCount = droppedEvents;
        }

        /// <summary>最近 128 条缓存决策，按发生顺序排列，旧记录自动淘汰。</summary>
        public RenderOrderOptions RenderOrder
        {
            get;
        }

        public int ReservedRenderOrders
        {
            get;
        }

        public IReadOnlyList<ViewCacheDiagnostic> CacheDiagnostics
        {
            get;
        }

        public bool IsShutdown
        {
            get;
        }

        public long CommitVersion
        {
            get;
        }

        /// <summary>表现重算与导航回调之外才为 true；false 时门控和覆盖来源可能处于过渡状态。</summary>
        public bool IsPresentationSettled
        {
            get;
        }

        public ViewHandle Focused
        {
            get;
        }

        public int TotalInstances
        {
            get;
        }

        public IReadOnlyList<ViewInstanceSnapshot> Instances
        {
            get;
        }

        public bool InstancesTruncated => Instances.Count < TotalInstances;

        public int HistoryCount
        {
            get;
        }

        /// <summary>最近一段历史，保留原先从旧到新的顺序。</summary>
        public IReadOnlyList<ViewHandle> RecentHistory
        {
            get;
        }

        public bool HistoryTruncated => RecentHistory.Count < HistoryCount;

        public int PendingRequestCount
        {
            get;
        }

        public int PostedRequestCount
        {
            get;
        }

        public int CachedViewCount
        {
            get;
        }

        public int RetiringCachedViewCount
        {
            get;
        }

        /// <summary>预加载占位数量，不等同于所有资源凭证或进程内存数量。</summary>
        public int PreloadReservationCount
        {
            get;
        }

        /// <summary>已超出关闭预算但尚未完成物理清理的实例数，不包含所有正常关闭中的实例。</summary>
        public int PendingCleanupCount
        {
            get;
        }

        /// <summary>历史保守标记：页面或缓存清理曾失败；重试成功不改写它。当前责任见 UnconfirmedCleanupCount。</summary>
        public bool HasCleanupFailure
        {
            get;
        }

        /// <summary>沿用原有历史标记语义；当前责任数量使用 UnconfirmedCleanupCount，历史失败使用 HasCleanupFailure。</summary>
        public bool HasUnconfirmedCleanup => HasCleanupFailure;

        /// <summary>宿主稳定身份，用于在组件销毁后从全局账本查询其责任。</summary>
        public Guid HostId
        {
            get;
        }

        /// <summary>采集时属于此宿主的在途或失败责任总数；显式重试确认成功后减少。</summary>
        public int UnconfirmedCleanupCount
        {
            get;
        }

        public IReadOnlyList<CleanupResponsibilitySnapshot> CleanupResponsibilities
        {
            get;
        }

        public bool CleanupResponsibilitiesTruncated => CleanupResponsibilities.Count < UnconfirmedCleanupCount;

        public long DroppedLifecycleEventCount
        {
            get;
        }
    }
}
