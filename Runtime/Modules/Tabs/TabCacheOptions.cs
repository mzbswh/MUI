using System;
using System.Collections.Generic;

namespace MUI.Tabs
{
    /// <summary>离开后的实例保留策略，所有缓存均受显式容量限制。</summary>
    public enum TabContentRetention
    {
        ReleaseOnLeave,
        CacheRecent,
        KeepSelectedTabs
    }

    /// <summary>不可变缓存配置；容量只计算已停用的缓存项，当前内容和有界在途操作另计。</summary>
    public sealed class TabCacheOptions
    {
        private readonly HashSet<string> keptTabs = new HashSet<string>(StringComparer.Ordinal);
        internal static readonly TabCacheOptions Disabled = new TabCacheOptions();

        public TabCacheOptions(TabContentRetention retention = TabContentRetention.ReleaseOnLeave,
                    int capacity = 0, IEnumerable<string> keptTabs = null, long? maxEstimatedBytes = null,
                    TimeSpan? timeToLive = null)
        {
            if (!Enum.IsDefined(typeof(TabContentRetention), retention))
            {
                throw new ArgumentOutOfRangeException(nameof(retention));
            }

            if (capacity < 0 || (retention != TabContentRetention.ReleaseOnLeave && capacity == 0) ||
                (retention == TabContentRetention.ReleaseOnLeave && capacity != 0))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (maxEstimatedBytes.HasValue &&
                (maxEstimatedBytes.Value <= 0 || retention == TabContentRetention.ReleaseOnLeave))
            {
                throw new ArgumentOutOfRangeException(nameof(maxEstimatedBytes));
            }

            if (timeToLive.HasValue &&
                (timeToLive.Value <= TimeSpan.Zero || retention == TabContentRetention.ReleaseOnLeave))
            {
                throw new ArgumentOutOfRangeException(nameof(timeToLive));
            }

            if (keptTabs != null)
            {
                if (retention != TabContentRetention.KeepSelectedTabs)
                {
                    throw new ArgumentException("Explicit keys require KeepSelectedTabs.", nameof(keptTabs));
                }

                foreach (var key in keptTabs)
                {
                    if (string.IsNullOrWhiteSpace(key) || !this.keptTabs.Add(key))
                    {
                        throw new ArgumentException("Cached Tab keys must be non-empty and unique.", nameof(keptTabs));
                    }
                }
            }

            if (retention == TabContentRetention.KeepSelectedTabs &&
                (this.keptTabs.Count == 0 || this.keptTabs.Count > capacity))
            {
                throw new ArgumentException("Selected Tab keys must fit the positive cache capacity.", nameof(keptTabs));
            }

            Retention = retention;
            Capacity = capacity;
            MaxEstimatedBytes = maxEstimatedBytes;
            TimeToLive = timeToLive;
        }

        public TabContentRetention Retention
        {
            get;
        }

        public int Capacity
        {
            get;
        }

        /// <summary>停用缓存项的估算字节上限；设置后拒绝大小未知的实例，不限制进程实际内存。</summary>
        public long? MaxEstimatedBytes
        {
            get;
        }

        /// <summary>实例每次进入停用缓存后的最长保留时间；null 不按时间过期，命中时仍复核有效性。</summary>
        public TimeSpan? TimeToLive
        {
            get;
        }

        internal bool Allows(string key) => Retention == TabContentRetention.CacheRecent ||
                    (Retention == TabContentRetention.KeepSelectedTabs && keptTabs.Contains(key));
    }
}
