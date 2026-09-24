using System;
using System.Collections.Generic;

namespace MUI
{
    /// <summary>一组标题和条目的结构快照；标题与条目模型均由业务层拥有。</summary>
    public sealed class ListGroup<T>
    {
        public ListGroup(object key, T header, IReadOnlyList<T> items, bool initiallyExpanded = true)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var copy = new T[items.Count];
            for (var index = 0; index < copy.Length; ++index)
            {
                copy[index] = items[index];
            }

            Header = header;
            Items = Array.AsReadOnly(copy);
            InitiallyExpanded = initiallyExpanded;
        }

        /// <summary>分组身份，用于重排或刷新后保留展开状态，与条目键属于不同空间。</summary>
        public object Key
        {
            get;
        }

        public T Header
        {
            get;
        }

        public IReadOnlyList<T> Items
        {
            get;
        }

        public bool InitiallyExpanded
        {
            get;
        }
    }
}
