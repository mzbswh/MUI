using System;
using System.Collections.Generic;

namespace MUI
{
    /// <summary>事务内的编辑器，Edit 回调返回后不能继续使用。</summary>
    public sealed class ObservableListEditor<T>
    {
        private readonly List<T> items;
        private readonly List<ListChange<T>> changes = new List<ListChange<T>>();
        private bool active = true;
        private readonly Action assertThread;

        internal ObservableListEditor(List<T> items, Action assertThread)
        {
            this.items = items;
            this.assertThread = assertThread;
        }

        public int Count
        {
            get
            {
                RequireActive();
                return items.Count;
            }
        }

        public T this[int index]
        {
            get
            {
                RequireActive();
                return items[index];
            }

            set => Replace(index, value);
        }

        internal ListChange<T>[] Complete()
        {
            active = false;
            return changes.ToArray();
        }

        internal void End()
        {
            active = false;
        }

        private void RequireActive()
        {
            assertThread();
            if (!active)
            {
                throw new InvalidOperationException("List editor is no longer active.");
            }
        }

        public void Add(T item) => Insert(Count, item);

        public void AddRange(IEnumerable<T> values) => InsertRange(Count, values);

        public void Insert(int index, T item) => InsertRange(index, new[] { item });

        public void InsertRange(int index, IEnumerable<T> values)
        {
            RequireActive();
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (index < 0 || index > items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var added = new List<T>(values).ToArray();
            if (added.Length == 0)
            {
                return;
            }

            items.InsertRange(index, added);
            changes.Add(new ListChange<T>(ListChangeKind.Add, index, Array.Empty<T>(), added));
        }

        public void RemoveAt(int index) => RemoveRange(index, 1);

        public void RemoveRange(int index, int count)
        {
            RequireActive();
            var removed = items.GetRange(index, count).ToArray();
            if (count == 0)
            {
                return;
            }

            items.RemoveRange(index, count);
            changes.Add(new ListChange<T>(ListChangeKind.Remove, index, removed, Array.Empty<T>()));
        }

        public void Replace(int index, T value)
        {
            RequireActive();
            var previous = items[index];
            items[index] = value;
            changes.Add(new ListChange<T>(ListChangeKind.Replace, index, new[] { previous }, new[] { value }));
        }

        public void NotifyUpdated(int index)
        {
            RequireActive();
            var value = items[index];
            changes.Add(new ListChange<T>(ListChangeKind.Update, index, new[] { value }, new[] { value }));
        }

        public void Move(int oldIndex, int newIndex)
        {
            RequireActive();
            if (newIndex < 0 || newIndex >= items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newIndex));
            }

            var item = items[oldIndex];
            if (oldIndex == newIndex)
            {
                return;
            }

            items.RemoveAt(oldIndex);
            items.Insert(newIndex, item);
            changes.Add(new ListChange<T>(ListChangeKind.Move, newIndex, new[] { item }, new[] { item }, oldIndex));
        }

        public void Reset(IEnumerable<T> values)
        {
            RequireActive();
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var next = new List<T>(values).ToArray();
            var previous = items.ToArray();
            items.Clear();
            items.AddRange(next);
            changes.Add(new ListChange<T>(ListChangeKind.Reset, 0, previous, next));
        }

        public void Clear() => Reset(Array.Empty<T>());
    }
}
