using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly long? maxCachedEstimatedBytes;
        private long reservedCacheEstimatedBytes;
        private long failedCacheEstimatedBytes;
        private readonly List<FailedCacheBudget> failedCacheBudgets = new List<FailedCacheBudget>();

        /// <summary>缓存、在途淘汰及释放失败所占估算额度；命中转为活动实例后移出本预算。</summary>
        public long ReservedCacheEstimatedBytes
        {
            get
            {
                AssertThread();
                return reservedCacheEstimatedBytes - ConfirmedFailedCacheEstimatedBytes;
            }
        }

        /// <summary>释放失败而未归还的估算额度；不表示框架能够测量实际残留内存。</summary>
        public long FailedCacheEstimatedBytes
        {
            get
            {
                AssertThread();
                return failedCacheEstimatedBytes - ConfirmedFailedCacheEstimatedBytes;
            }
        }

        // 诊断读取只计算值；已确认的记录由下一次缓存维护或准入移除，不从查询启动工作。
        private long ConfirmedFailedCacheEstimatedBytes
        {
            get
            {
                long confirmed = 0;
                foreach (var entry in failedCacheBudgets)
                {
                    if (entry.Content.IsCleanupConfirmed)
                    {
                        confirmed += entry.EstimatedBytes;
                    }
                }
                return confirmed;
            }
        }

        private void RefreshFailedCacheBudget()
        {
            for (var index = failedCacheBudgets.Count - 1; index >= 0; --index)
            {
                var entry = failedCacheBudgets[index];
                if (!entry.Content.IsCleanupConfirmed)
                {
                    continue;
                }
                reservedCacheEstimatedBytes -= entry.EstimatedBytes;
                failedCacheEstimatedBytes -= entry.EstimatedBytes;
                failedCacheBudgets.RemoveAt(index);
            }
        }

        private bool TryMakeCacheRoom(Route route)
        {
            RefreshFailedCacheBudget();
            if (maxCachedEstimatedBytes.HasValue && !route.EstimatedRetainedBytes.HasValue)
            {
                return false;
            }

            var estimate = route.EstimatedRetainedBytes ?? 0;
            var limit = maxCachedEstimatedBytes ?? long.MaxValue;
            if (estimate > limit)
            {
                return false;
            }

            if (cachedContents.Count < cacheCapacity && estimate <= limit - reservedCacheEstimatedBytes)
            {
                return true;
            }

            if (IsCacheRetiring)
            {
                return false;
            }

            var removeCount = 0;
            long reclaimable = 0;
            while (removeCount < cachedContents.Count &&
                (cachedContents.Count - removeCount >= cacheCapacity ||
                 estimate > limit - (reservedCacheEstimatedBytes - reclaimable)))
            {
                reclaimable += cachedContents[removeCount].Route.EstimatedRetainedBytes ?? 0;
                removeCount++;
            }

            // 在途或失败额度导致即使清空目录也不足时，保留现有可用内容。
            if (estimate > limit - (reservedCacheEstimatedBytes - reclaimable))
            {
                return false;
            }

            var retiring = cachedContents.GetRange(0, removeCount).ToArray();
            cachedContents.RemoveRange(0, removeCount);
            BeginCacheRetirement(retiring);

            // 移出目录不等于内存已释放；不等待淘汰，也不提前借用其尚未归还的额度。
            // 最终销毁回调可以请求宿主退出，因此接纳前同时复核宿主状态。
            return !IsShutdown && !IsCacheClearing &&
                cachedContents.Count < cacheCapacity && estimate <= limit - reservedCacheEstimatedBytes;
        }

        private sealed class FailedCacheBudget
        {
            internal CachedViewContent Content;
            internal long EstimatedBytes;
        }
    }
}
