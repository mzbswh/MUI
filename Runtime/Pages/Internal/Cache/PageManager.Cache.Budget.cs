using MUI.Pages.Diagnostics;
using System;
using System.Collections.Generic;

namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        private readonly PageCacheBudget cacheBudget;
        public long ReservedCacheEstimatedBytes
        {
            get
            {
                AssertThread();
                return cacheBudget.Reserved;
            }
        }
        public long FailedCacheEstimatedBytes
        {
            get
            {
                AssertThread();
                return cacheBudget.Failed;
            }
        }

        private bool TryMakeCacheRoom(PageDefinition route)
        {
            cacheBudget.Refresh();
            if (cacheBudget.Limit.HasValue && !route.EstimatedRetainedBytes.HasValue)
            {
                return RejectCache(route, PageCacheDecision.EstimateRequired);
            }

            var estimate = route.EstimatedRetainedBytes ?? 0;
            var limit = cacheBudget.Limit ?? long.MaxValue;
            if (estimate > limit)
            {
                return RejectCache(route, PageCacheDecision.BudgetExceeded);
            }

            if (cachedContents.Count < cacheCapacity && estimate <= limit - cacheBudget.Committed)
            {
                return true;
            }

            if (IsCacheRetiring)
            {
                return RejectCache(route, PageCacheDecision.RetirementPending);
            }

            var removeCount = cachedContents.PlanEviction(estimate, limit, cacheBudget.Committed);
            if (removeCount < 0)
                return RejectCache(route, PageCacheDecision.BudgetExceeded);
            var retiring = cachedContents.EvictOldest(removeCount);
            foreach (var entry in retiring)
            {
                RecordCacheDecision(entry.Definition, PageCacheDecision.EvictedForCapacity);
            }
            BeginCacheRetirement(retiring);

            // 移出目录不等于内存已释放；不等待淘汰，也不提前借用其尚未归还的额度。
            // 最终销毁回调可以请求宿主退出，因此接纳前同时复核宿主状态。
            if (IsShutdown || IsCacheClearing)
            {
                return RejectCache(route, PageCacheDecision.HostUnavailable);
            }
            return (cachedContents.Count < cacheCapacity && estimate <= limit - cacheBudget.Committed) ||
                RejectCache(route, PageCacheDecision.RetirementPending);
        }


    }
}
