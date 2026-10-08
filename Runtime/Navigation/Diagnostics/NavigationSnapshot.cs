using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    /// <summary>所属 UI 线程上的导航状态快照；只包含已复制的元数据，不执行项目或渲染器回调。</summary>
    public sealed class NavigationSnapshot
    {
        internal NavigationSnapshot(bool shutdown, long version, bool presentationSettled,
                    ViewHandle focused, int totalInstances, ViewInstanceSnapshot[] instances, int historyCount,
                    ViewHandle[] history, int pendingRequests, int postedRequests, int cachedViews,
                    int retiringCachedViews, int preloadReservations, int pendingCleanup,
                    bool hasUnconfirmedCleanup, long droppedEvents)
        {
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
            HasUnconfirmedCleanup = hasUnconfirmedCleanup;
            DroppedLifecycleEventCount = droppedEvents;
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

        /// <summary>页面或缓存清理曾失败，不能据此确认其所有资源均已归还。</summary>
        public bool HasUnconfirmedCleanup
        {
            get;
        }

        public long DroppedLifecycleEventCount
        {
            get;
        }
    }
}
