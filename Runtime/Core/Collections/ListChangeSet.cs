using System;
using System.Collections.Generic;

namespace MUI
{
    public enum ListChangeKind
    {
        Add,
        Remove,
        Replace,
        Update,
        Move,
        Reset
    }

    /// <summary>索引相对于本次变更前的中间列表状态。</summary>
    public sealed class ListChange<T>
    {
        internal ListChange(ListChangeKind kind, int index, T[] previous, T[] current, int oldIndex = -1)
        {
            Kind = kind;
            Index = index;
            OldIndex = oldIndex;
            PreviousItems = Array.AsReadOnly(previous);
            CurrentItems = Array.AsReadOnly(current);
        }

        public ListChangeKind Kind
        {
            get;
        }

        public int Index
        {
            get;
        }

        /// <summary>移动来源索引；Index 表示移除后的最终目标位置。</summary>
        public int OldIndex
        {
            get;
        }

        public IReadOnlyList<T> PreviousItems
        {
            get;
        }

        public IReadOnlyList<T> CurrentItems
        {
            get;
        }
    }

    public sealed class ListChangeSet<T>
    {
        internal ListChangeSet(long before, long after, int oldCount, int count, ListChange<T>[] changes)
        {
            PreviousVersion = before;
            Version = after;
            PreviousCount = oldCount;
            Count = count;
            Changes = Array.AsReadOnly(changes);
        }

        public long PreviousVersion
        {
            get;
        }

        public long Version
        {
            get;
        }

        public int PreviousCount
        {
            get;
        }

        public int Count
        {
            get;
        }

        public IReadOnlyList<ListChange<T>> Changes
        {
            get;
        }
    }

    public interface IReadOnlyObservableList<T> : IReadOnlyList<T>
    {
        event Action<ListChangeSet<T>> Changed;

        long Version
        {
            get;
        }
    }
}
