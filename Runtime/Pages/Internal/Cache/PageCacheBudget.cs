using System.Collections.Generic;
namespace MUI.Pages
{
    internal sealed class PageCacheBudget
    {
        internal long? Limit
        {
            get;
        }
        internal PageCacheBudget(long? limit)
        {
            Limit = limit;
        }
        internal long Committed => reservedCacheEstimatedBytes;
        private long reservedCacheEstimatedBytes;
        private long failedCacheEstimatedBytes;
        private readonly List<FailedCacheBudget> failedCacheBudgets = new List<FailedCacheBudget>();

        /// <summary>缓存、在途淘汰及释放失败所占估算额度；命中转为活动实例后移出本预算。</summary>
        internal long Reserved
        {
            get
            {
                return reservedCacheEstimatedBytes - ConfirmedFailedCacheEstimatedBytes;
            }
        }

        /// <summary>释放失败而未归还的估算额度；不表示框架能够测量实际残留内存。</summary>
        internal long Failed
        {
            get
            {
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

        internal void Refresh()
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

        internal void Reserve(long bytes) => reservedCacheEstimatedBytes += bytes;
        internal void Release(long bytes) => reservedCacheEstimatedBytes -= bytes;
        internal void RecordFailure(CachedViewContent content, long bytes)
        {
            failedCacheEstimatedBytes += bytes;
            if (bytes != 0)
                failedCacheBudgets.Add(new FailedCacheBudget { Content = content, EstimatedBytes = bytes });
        }
        private sealed class FailedCacheBudget
        {
            internal CachedViewContent Content;
            internal long EstimatedBytes;
        }
    }
}
