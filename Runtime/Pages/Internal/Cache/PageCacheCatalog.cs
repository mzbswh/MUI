using System.Collections.Generic;
namespace MUI.Pages
{
    internal sealed class PageCacheCatalog
    {
        private readonly List<PageCacheEntry> entries = new List<PageCacheEntry>();
        internal PageCacheCatalog(int capacity)
        {
            Capacity = capacity;
        }
        internal int Capacity
        {
            get;
        }
        internal int Count => entries.Count;
        internal PageCacheEntry this[int index] => entries[index];
        internal void Add(PageCacheEntry entry) => entries.Add(entry);
        internal void RemoveAt(int index) => entries.RemoveAt(index);
        internal void Clear() => entries.Clear();
        internal PageCacheEntry[] ToArray() => entries.ToArray();

        // -1 means in-flight or failed releases prevent admission even after evicting all entries.
        internal int PlanEviction(long estimate, long limit, long reserved)
        {
            var count = 0;
            long reclaimable = 0;
            while (count < entries.Count && (entries.Count - count >= Capacity || estimate > limit - (reserved - reclaimable)))
                reclaimable += entries[count++].Definition.EstimatedRetainedBytes ?? 0;
            return estimate > limit - (reserved - reclaimable) ? -1 : count;
        }

        internal PageCacheEntry[] EvictOldest(int count)
        {
            var result = entries.GetRange(0, count).ToArray();
            entries.RemoveRange(0, count);
            return result;
        }
    }
    internal sealed class PageCacheEntry
    {
        public PageDefinition Definition;
        public CachedViewContent Content;
        public long CachedAt;
    }
}
