using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI.Samples.Navigation
{
    public sealed partial class PagedList<T, TCursor>
    {
        private PageLoadResult CommitPage(ListPage<T, TCursor> page, int requested, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (page == null || page.Items.Count > requested || (page.Items.Count == 0 && page.HasMore))
            {
                throw new InvalidOperationException("Page must fit its requested size; an empty page must end pagination.");
            }

            if (page.HasMore && EqualityComparer<TCursor>.Default.Equals(page.NextCursor, cursor))
            {
                throw new InvalidOperationException("Pagination cursor did not advance.");
            }

            var addedKeys = new List<object>(page.Items.Count);
            var addedSet = new HashSet<object>();
            foreach (var item in page.Items)
            {
                var key = itemKey(item);
                // 即使某个旧条目即将被淘汰，本页也不能以同一个键悄悄替换它。
                if (key == null || keys.Contains(key) || !addedSet.Add(key))
                {
                    throw new InvalidOperationException("Paged item keys must be non-null and unique in the retained window and new page.");
                }

                addedKeys.Add(key);
            }

            // 不计算 count + added，避免大容量配置的整型溢出。
            var evicted = Math.Max(0, items.Count - (maxItems - page.Items.Count));
            if (evicted != 0 && OverflowPolicy != PageOverflowPolicy.EvictOppositeEnd)
            {
                throw new InvalidOperationException("Paged list item capacity is exhausted.");
            }

            var retainedCount = keyOrder.Count - evicted;
            var nextKeyOrder = new List<object>(retainedCount + addedKeys.Count);
            if (Insertion == PageInsertion.Prepend)
            {
                nextKeyOrder.AddRange(addedKeys);
                nextKeyOrder.AddRange(keyOrder.GetRange(0, retainedCount));
            }
            else
            {
                nextKeyOrder.AddRange(keyOrder.GetRange(evicted, retainedCount));
                nextKeyOrder.AddRange(addedKeys);
            }

            var nextKeys = new HashSet<object>(nextKeyOrder);
            if (nextKeys.Count != nextKeyOrder.Count)
            {
                throw new InvalidOperationException("Paged item keys must remain stable and unique while retained.");
            }

            // 不重新调用旧条目的键选择器，避免模型修改后不能准确归还原来的键记录。
            // 项目回调及所有候选分配结束后再复核取消，发布前不修改现有数据。
            token.ThrowIfCancellationRequested();
            var previousCursor = cursor;
            var previousHasMore = HasMore;
            var previousKeys = keys;
            var previousKeyOrder = keyOrder;
            cursor = page.NextCursor;
            HasMore = page.HasMore;
            IsLoading = false;
            keys = nextKeys;
            keyOrder = nextKeyOrder;
            try
            {
                // 删除和插入构成同一版本通知，观察者不会看到容量超限或半个数据窗口。
                items.Edit(editor =>
                {
                    if (evicted != 0)
                    {
                        editor.RemoveRange(Insertion == PageInsertion.Prepend ? retainedCount : 0, evicted);
                    }

                    editor.InsertRange(Insertion == PageInsertion.Prepend ? 0 : editor.Count, page.Items);
                });
            }
            catch
            {
                cursor = previousCursor;
                HasMore = previousHasMore;
                keys = previousKeys;
                keyOrder = previousKeyOrder;
                throw;
            }

            // 集合发布期间的关闭不撤销已接受的数据页；淘汰仅释放来源持有的模型引用。
            return new PageLoadResult(PageLoadStatus.Loaded, page.Items.Count, evictedCount: evicted);
        }
    }
}
