using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MUI
{
    /// <summary>
    /// 将有界森林按先序展平为可观察列表，折叠后跳过整个子树。
    /// 结构变化显式 Reset，不订阅或销毁节点模型，渲染后端仍消费普通条目集合。
    /// </summary>
    public sealed partial class TreeList<T> : IReadOnlyObservableList<T>
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Func<T, object> itemKey;
        private readonly ObservableList<T> visible = new ObservableList<T>();
        private Snapshot snapshot = new Snapshot(Array.Empty<Entry>(), new Dictionary<object, int>(), Array.Empty<bool>());
        private bool editing;

        /// <param name="maxNodes">包含隐藏子树在内的全部节点上限。</param>
        /// <param name="maxDepth">最大节点深度，根深度为 0；验证与遍历均不使用递归。</param>
        public TreeList(Func<T, object> itemKey, int maxNodes = 10000, int maxDepth = 64)
        {
            this.itemKey = itemKey ?? throw new ArgumentNullException(nameof(itemKey));
            if (maxNodes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNodes));
            }

            if (maxDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth));
            }

            Capacity = maxNodes;
            MaxDepth = maxDepth;
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

        public int MaxDepth
        {
            get;
        }

        public int Count => visible.Count;

        /// <summary>可见集合版本；仅修改隐藏节点的展开状态不会递增。</summary>
        public long Version => visible.Version;

        public T this[int index] => visible[index];

        public int TotalNodeCount
        {
            get
            {
                RequireThread();
                return snapshot.Entries.Length;
            }
        }

        public TreeNode<T> GetNode(object key) => snapshot.Entries[FindNode(key)].Node;

        public int GetDepth(object key) => snapshot.Entries[FindNode(key)].Depth;

        public bool IsExpanded(object key) => snapshot.Expanded[FindNode(key)];

        /// <summary>存在但被祖先折叠的节点返回 -1；未知键报错。</summary>
        public int GetVisibleIndex(object key) => snapshot.VisibleIndices[FindNode(key)];

        public bool HasChildren(object key)
        {
            var index = FindNode(key);
            return snapshot.Entries[index].End > index + 1;
        }

        public object GetParentKey(object key)
        {
            var parent = snapshot.Entries[FindNode(key)].Parent;
            return parent < 0 ? null : snapshot.Entries[parent].Key;
        }

        /// <summary>原子替换整棵森林，默认按节点键保留展开状态；根与同级节点遵循输入顺序。</summary>
        public void Reset(IEnumerable<TreeNode<T>> nodes, bool preserveExpansion = true)
        {
            BeginEdit();
            try
            {
                Commit(BuildSnapshot(nodes, preserveExpansion));
            }
            finally
            {
                editing = false;
            }
        }

        /// <summary>
        /// 折叠只改变可见集合，保留后代自己的展开状态供重新展开时恢复。
        /// 一次增量通知更新可见父行并插入或移除后代；隐藏节点的状态变化不发布可见行通知。
        /// </summary>
        public bool SetExpanded(object key, bool expanded)
        {
            BeginEdit();
            try
            {
                var index = FindNode(key);
                if (snapshot.Expanded[index] == expanded)
                {
                    return false;
                }

                var candidate = (bool[])snapshot.Expanded.Clone();
                candidate[index] = expanded;
                Commit(new Snapshot(snapshot.Entries, snapshot.Indices, candidate), reset: false);
                return true;
            }
            finally
            {
                editing = false;
            }
        }

        /// <summary>一次事务展开目标的全部祖先，让隐藏节点具备滚动定位资格；不改变目标自身状态。</summary>
        public bool ExpandAncestors(object key)
        {
            BeginEdit();
            try
            {
                var parent = snapshot.Entries[FindNode(key)].Parent;
                var candidate = (bool[])snapshot.Expanded.Clone();
                var changed = false;
                while (parent >= 0)
                {
                    changed |= !candidate[parent];
                    candidate[parent] = true;
                    parent = snapshot.Entries[parent].Parent;
                }

                if (changed)
                {
                    Commit(new Snapshot(snapshot.Entries, snapshot.Indices, candidate), reset: false);
                }

                return changed;
            }
            finally
            {
                editing = false;
            }
        }

        /// <summary>在一次批量通知中展开或折叠全部节点，保留仍可见条目的身份与相对顺序。</summary>
        public void SetAllExpanded(bool expanded)
        {
            BeginEdit();
            try
            {
                var candidate = new bool[snapshot.Expanded.Length];
                var changed = false;
                for (var index = 0; index < candidate.Length; ++index)
                {
                    changed |= snapshot.Expanded[index] != expanded;
                    candidate[index] = expanded;
                }

                if (changed)
                {
                    Commit(new Snapshot(snapshot.Entries, snapshot.Indices, candidate), reset: false);
                }
            }
            finally
            {
                editing = false;
            }
        }

        public void Clear() => Reset(Array.Empty<TreeNode<T>>());

        private void Commit(Snapshot candidate, bool reset = true)
        {
            var rows = new List<T>();
            for (var index = 0; index < candidate.Entries.Length;)
            {
                var entry = candidate.Entries[index];
                candidate.VisibleIndices[index] = rows.Count;
                rows.Add(entry.Node.Item);
                index = candidate.Expanded[index] ? index + 1 : entry.End;
            }

            var previous = snapshot;
            snapshot = candidate;
            try
            {
                // 先切换元数据，再发布相同版本的可见行；展开变化不使未改动行的测量失效。
                if (reset)
                {
                    visible.Reset(rows);
                }
                else
                {
                    visible.Edit(editor => ApplyExpansionChanges(previous, candidate, rows, editor));
                }
            }
            catch
            {
                snapshot = previous;
                throw;
            }
        }

        private int FindNode(object key)
        {
            RequireThread();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!snapshot.Indices.TryGetValue(key, out var index))
            {
                throw new ArgumentException("Node key is not in this tree.", nameof(key));
            }

            return index;
        }

        private void BeginEdit()
        {
            RequireThread();
            if (editing)
            {
                throw new InvalidOperationException("Cannot edit tree data from its callbacks or collection notifications.");
            }

            editing = true;
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Tree list must be accessed on its owning UI thread.");
            }
        }

        public IEnumerator<T> GetEnumerator() => visible.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Entry
        {
            internal object Key;
            internal TreeNode<T> Node;
            internal int Parent;
            internal int Depth;
            internal int End;
        }

        private sealed class Snapshot
        {
            internal readonly Entry[] Entries;
            internal readonly Dictionary<object, int> Indices;
            internal readonly bool[] Expanded;
            internal readonly int[] VisibleIndices;

            internal Snapshot(Entry[] entries, Dictionary<object, int> indices, bool[] expanded)
            {
                Entries = entries;
                Indices = indices;
                Expanded = expanded;
                VisibleIndices = new int[entries.Length];
                Array.Fill(VisibleIndices, -1);
            }
        }
    }
}
