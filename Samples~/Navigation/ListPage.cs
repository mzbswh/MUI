using System;
using System.Collections.Generic;

namespace MUI.Samples.Navigation
{
    /// <summary>分页提供者返回的不可变结构快照；其中的条目模型仍由业务方拥有。</summary>
    public sealed class ListPage<T, TCursor>
    {
        public ListPage(IReadOnlyList<T> items, TCursor nextCursor = default, bool hasMore = false)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var copy = new T[items.Count];
            for (var index = 0; index < copy.Length; ++index)
            {
                copy[index] = items[index];
            }

            Items = Array.AsReadOnly(copy);
            NextCursor = nextCursor;
            HasMore = hasMore;
        }

        public IReadOnlyList<T> Items
        {
            get;
        }

        public TCursor NextCursor
        {
            get;
        }

        public bool HasMore
        {
            get;
        }
    }
}
