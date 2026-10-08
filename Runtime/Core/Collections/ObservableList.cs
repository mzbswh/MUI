using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MUI
{
    /// <summary>UI 线程集合，提供原子结构编辑和有序批次通知。</summary>
    public sealed class ObservableList<T> : IReadOnlyObservableList<T>
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Func<T, object> itemKey;
        private readonly IEqualityComparer<object> keyComparer;
        private List<T> items;
        private HashSet<object> keys;
        private bool editing;
        private bool notifying;
        private long version;

        public ObservableList(IEnumerable<T> initial = null, Func<T, object> itemKey = null, IEqualityComparer<object> keyComparer = null)
        {
            this.itemKey = itemKey;
            this.keyComparer = keyComparer ?? EqualityComparer<object>.Default;
            items = initial == null ? new List<T>() : new List<T>(initial);
            keys = ValidateKeys(items);
        }

        public event Action<ListChangeSet<T>> Changed;

        public int Count
        {
            get
            {
                AssertThread();
                return items.Count;
            }
        }

        public long Version
        {
            get
            {
                AssertThread();
                return version;
            }
        }

        public T this[int index]
        {
            get
            {
                AssertThread();
                return items[index];
            }

            set => Edit(editor => editor.Replace(index, value));
        }

        public void Add(T item)
        {
            AssertThread();
            RequireEditable();
            editing = true;
            ListChangeSet<T> changeSet;
            try
            {
                var nextVersion = checked(version + 1);
                var key = itemKey == null ? null : itemKey(item);
                if (itemKey != null && (key == null || keys.Contains(key)))
                {
                    throw new InvalidOperationException("Item keys must be non-null and unique.");
                }

                var count = items.Count;
                var nextCount = checked(count + 1);
                changeSet = new ListChangeSet<T>(version, nextVersion, count, nextCount,
                    new[] { new ListChange<T>(ListChangeKind.Add, count, Array.Empty<T>(), new[] { item }) });
                // 改变键成员关系前先分配容量，确保下方 Add 不需要扩容。
                if (items.Capacity < nextCount)
                {
                    items.Capacity = (int)Math.Min(int.MaxValue, Math.Max(4L, (long)nextCount * 2));
                }

                if (keys != null && !keys.Add(key))
                {
                    throw new InvalidOperationException("Item keys must be non-null and unique.");
                }

                items.Add(item);
                version = nextVersion;
            }
            finally
            {
                editing = false;
            }

            Publish(changeSet);
        }

        public void AddRange(IEnumerable<T> values) => Edit(editor => editor.AddRange(values));

        public void Insert(int index, T item) => Edit(editor => editor.Insert(index, item));

        public void RemoveAt(int index) => Edit(editor => editor.RemoveAt(index));

        public void RemoveRange(int index, int count) => Edit(editor => editor.RemoveRange(index, count));

        public void Move(int oldIndex, int newIndex)
        {
            AssertThread();
            RequireEditable();
            if (newIndex < 0 || newIndex >= items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newIndex));
            }

            var item = items[oldIndex];
            if (oldIndex == newIndex)
            {
                return;
            }

            var nextVersion = checked(version + 1);
            var change = new ListChangeSet<T>(version, nextVersion, items.Count, items.Count,
                new[] { new ListChange<T>(ListChangeKind.Move, newIndex, new[] { item }, new[] { item }, oldIndex) });
            // 只移动受影响范围，数量和稳定键成员关系不变。
            if (oldIndex < newIndex)
            {
                for (var index = oldIndex; index < newIndex; ++index)
                {
                    items[index] = items[index + 1];
                }
            }
            else
            {
                for (var index = oldIndex; index > newIndex; --index)
                {
                    items[index] = items[index - 1];
                }
            }

            items[newIndex] = item;
            version = nextVersion;
            Publish(change);
        }

        public void NotifyUpdated(int index)
        {
            AssertThread();
            RequireEditable();
            var item = items[index];
            var nextVersion = checked(version + 1);
            var change = new ListChangeSet<T>(version, nextVersion, items.Count, items.Count,
                new[] { new ListChange<T>(ListChangeKind.Update, index, new[] { item }, new[] { item }) });
            version = nextVersion;
            Publish(change);
        }

        public void Reset(IEnumerable<T> values) => Edit(editor => editor.Reset(values));

        public void Clear() => Edit(editor => editor.Clear());

        public void Edit(Action<ObservableListEditor<T>> edit)
        {
            AssertThread();
            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            RequireEditable();
            editing = true;
            ObservableListEditor<T> editor = null;
            ListChangeSet<T> changeSet = null;
            try
            {
                var candidate = new List<T>(items);
                editor = new ObservableListEditor<T>(candidate, AssertThread);
                edit(editor);
                var changes = editor.Complete();
                if (changes.Length == 0)
                {
                    return;
                }

                var candidateKeys = ValidateKeys(candidate);
                var nextVersion = checked(version + 1);
                changeSet = new ListChangeSet<T>(version, nextVersion, items.Count, candidate.Count, changes);
                items = candidate;
                keys = candidateKeys;
                version = nextVersion;
            }
            finally
            {
                if (editor != null)
                {
                    editor.End();
                }

                editing = false;
            }

            Publish(changeSet);
        }

        private void Publish(ListChangeSet<T> changeSet)
        {
            notifying = true;
            try
            {
                var handlers = Changed;
                if (handlers != null)
                {
                    foreach (Action<ListChangeSet<T>> handler in handlers.GetInvocationList())
                    {
                        try
                        {
                            handler(changeSet);
                        }
                        catch (Exception error)
                        {
                            UIErrors.Report(error);
                        }
                    }
                }
            }
            finally
            {
                notifying = false;
            }
        }

        private HashSet<object> ValidateKeys(List<T> values)
        {
            if (itemKey == null)
            {
                return null;
            }

            var keys = new HashSet<object>(keyComparer);
            foreach (var value in values)
            {
                var key = itemKey(value);
                if (key == null || !keys.Add(key))
                {
                    throw new InvalidOperationException("Item keys must be non-null and unique.");
                }
            }

            return keys;
        }

        private void RequireEditable()
        {
            if (editing || notifying)
            {
                throw new InvalidOperationException("Reentrant collection edits are not supported. Schedule a later update.");
            }
        }

        private void AssertThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("ObservableList must be accessed on its owning UI thread.");
            }
        }

        public List<T>.Enumerator GetEnumerator()
        {
            AssertThread();
            return items.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
