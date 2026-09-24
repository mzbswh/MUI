using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MUI
{
    /// <summary>
    /// 将分组快照展平成可观察列表，标题始终可见，折叠时移除该组正文。
    /// 不依赖渲染后端；不订阅或销毁业务模型，结构更新通过 Reset 显式提交。
    /// </summary>
    public sealed class GroupedList<T> : IReadOnlyObservableList<T>
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Func<T, object> itemKey;
        private readonly ObservableList<T> visible = new ObservableList<T>();
        private List<GroupState> groups = new List<GroupState>();
        private Dictionary<object, int> groupIndices = new Dictionary<object, int>();
        private int totalItemCount;
        private bool editing;

        /// <param name="maxItems">包含全部标题及折叠正文在内的条目上限。</param>
        /// <param name="maxGroups">分组数量上限，展开状态目录不会永久保留已删除的组。</param>
        public GroupedList(Func<T, object> itemKey, int maxItems = 10000, int maxGroups = 256)
        {
            this.itemKey = itemKey ?? throw new ArgumentNullException(nameof(itemKey));
            if (maxItems < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxItems));
            }

            if (maxGroups < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxGroups));
            }

            Capacity = maxItems;
            GroupCapacity = maxGroups;
        }

        public event Action<ListChangeSet<T>> Changed
        {
            add
            {
                RequireThread();
                visible.Changed += value;
            }
            remove
            {
                RequireThread();
                visible.Changed -= value;
            }
        }

        public int Capacity
        {
            get;
        }

        public int GroupCapacity
        {
            get;
        }

        public int Count => visible.Count;

        public long Version => visible.Version;

        public T this[int index] => visible[index];

        public int GroupCount
        {
            get
            {
                RequireThread();
                return groups.Count;
            }
        }

        /// <summary>所有标题与正文的数量，包含当前折叠而未输出的条目。</summary>
        public int TotalItemCount
        {
            get
            {
                RequireThread();
                return totalItemCount;
            }
        }

        public ListGroup<T> GetGroup(int index)
        {
            RequireThread();
            return groups[index].Group;
        }

        public bool IsExpanded(object groupKey)
        {
            RequireThread();
            return groups[FindGroup(groupKey)].Expanded;
        }

        /// <summary>
        /// 原子替换全部分组；默认按组键保留展开状态，新组采用自身初始配置。
        /// 标题和所有正文的条目键必须全局唯一，折叠正文也参与校验。
        /// </summary>
        public void Reset(IEnumerable<ListGroup<T>> values, bool preserveExpansion = true)
        {
            BeginEdit();
            try
            {
                if (values == null)
                {
                    throw new ArgumentNullException(nameof(values));
                }

                var candidate = new List<GroupState>();
                var indices = new Dictionary<object, int>();
                var keys = new HashSet<object>();
                var total = 0;
                foreach (var group in values)
                {
                    if (group == null || candidate.Count == GroupCapacity)
                    {
                        throw new InvalidOperationException("Groups must be non-null and fit the configured group capacity.");
                    }

                    // 先比较剩余量，不进行可能溢出的条目数相加。
                    if (total == Capacity || group.Items.Count > Capacity - total - 1)
                    {
                        throw new InvalidOperationException("Group headers and items exceed the configured item capacity.");
                    }

                    if (!indices.TryAdd(group.Key, candidate.Count))
                    {
                        throw new InvalidOperationException("Group keys must be unique.");
                    }

                    ValidateKey(group.Header, keys);
                    foreach (var item in group.Items)
                    {
                        ValidateKey(item, keys);
                    }

                    var expanded = preserveExpansion && groupIndices.TryGetValue(group.Key, out var oldIndex)
                        ? groups[oldIndex].Expanded : group.InitiallyExpanded;
                    candidate.Add(new GroupState(group, expanded));
                    total += group.Items.Count + 1;
                }

                var rows = Flatten(candidate);
                Commit(candidate, indices, total, editor => editor.Reset(rows));
            }
            finally
            {
                editing = false;
            }
        }

        /// <summary>单次通知中插入或移除正文，标题与其他组保持原有身份；未知组键会报错。</summary>
        public bool SetExpanded(object groupKey, bool expanded)
        {
            BeginEdit();
            try
            {
                var index = FindGroup(groupKey);
                var current = groups[index];
                if (current.Expanded == expanded)
                {
                    return false;
                }

                var row = 1;
                for (var previous = 0; previous < index; ++previous)
                {
                    row += 1 + (groups[previous].Expanded ? groups[previous].Group.Items.Count : 0);
                }

                var candidate = new List<GroupState>(groups);
                candidate[index] = new GroupState(current.Group, expanded);
                Commit(candidate, groupIndices, totalItemCount, editor =>
                {
                    // 即使为空组也发布标题更新，观察者可据此同步展开图标等状态。
                    editor.NotifyUpdated(row - 1);
                    if (expanded)
                    {
                        editor.InsertRange(row, current.Group.Items);
                    }
                    else
                    {
                        editor.RemoveRange(row, current.Group.Items.Count);
                    }
                });
                return true;
            }
            finally
            {
                editing = false;
            }
        }

        /// <summary>一次增量事务展开或折叠所有分组，只更新变化的标题并插入或移除正文。</summary>
        public void SetAllExpanded(bool expanded)
        {
            BeginEdit();
            try
            {
                var candidate = new List<GroupState>(groups.Count);
                var changed = false;
                foreach (var state in groups)
                {
                    changed |= state.Expanded != expanded;
                    candidate.Add(new GroupState(state.Group, expanded));
                }

                if (changed)
                {
                    // Commit 会先切换 groups，闭包明确保留旧快照以比较展开状态。
                    var previous = groups;
                    Commit(candidate, groupIndices, totalItemCount, editor =>
                    {
                        var row = 0;
                        foreach (var state in previous)
                        {
                            if (state.Expanded != expanded)
                            {
                                editor.NotifyUpdated(row);
                                if (expanded)
                                {
                                    editor.InsertRange(row + 1, state.Group.Items);
                                }
                                else
                                {
                                    editor.RemoveRange(row + 1, state.Group.Items.Count);
                                }
                            }

                            // 索引始终对应本次编辑的中间状态，而不是编辑前的分组偏移。
                            row += 1 + (expanded ? state.Group.Items.Count : 0);
                        }
                    });
                }
            }
            finally
            {
                editing = false;
            }
        }

        public void Clear() => Reset(Array.Empty<ListGroup<T>>());

        private void ValidateKey(T item, HashSet<object> keys)
        {
            var key = itemKey(item);
            if (key == null || !keys.Add(key))
            {
                throw new InvalidOperationException("Header and item keys must be non-null, stable and globally unique.");
            }
        }

        private static List<T> Flatten(List<GroupState> states)
        {
            var rows = new List<T>();
            foreach (var state in states)
            {
                rows.Add(state.Group.Header);
                if (state.Expanded)
                {
                    rows.AddRange(state.Group.Items);
                }
            }

            return rows;
        }

        private void Commit(List<GroupState> candidate, Dictionary<object, int> indices,
                    int total, Action<ObservableListEditor<T>> edit)
        {
            var previous = groups;
            var previousIndices = groupIndices;
            var previousTotal = totalItemCount;
            groups = candidate;
            groupIndices = indices;
            totalItemCount = total;
            try
            {
                // 先切换组状态再发布集合，事件读取者看到相同版本的展开状态和可见行。
                visible.Edit(edit);
            }
            catch
            {
                groups = previous;
                groupIndices = previousIndices;
                totalItemCount = previousTotal;
                throw;
            }
        }

        private int FindGroup(object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!groupIndices.TryGetValue(key, out var index))
            {
                throw new ArgumentException("Group key is not in this list.", nameof(key));
            }

            return index;
        }

        private void BeginEdit()
        {
            RequireThread();
            if (editing)
            {
                throw new InvalidOperationException("Cannot edit grouped data from its callbacks or collection notifications.");
            }

            editing = true;
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Grouped list must be accessed on its owning UI thread.");
            }
        }

        public IEnumerator<T> GetEnumerator() => visible.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class GroupState
        {
            internal readonly ListGroup<T> Group;
            internal readonly bool Expanded;

            internal GroupState(ListGroup<T> group, bool expanded)
            {
                Group = group;
                Expanded = expanded;
            }
        }
    }
}
