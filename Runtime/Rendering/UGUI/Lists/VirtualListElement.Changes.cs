using System;
using System.Collections.Generic;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private bool CanApplyIncrementally(ListChangeSet<VirtualListItem> change, ChangeContext context)
        {
            if (!IsCurrentChange(context) || change == null || Error != null)
            {
                return false;
            }

            var version = context.Source.Version;
            var count = context.Source.Count;
            var currentVersion = context.Source.Version;
            return IsCurrentChange(context) && change.PreviousVersion == snapshotVersion &&
                version == currentVersion && change.Version == currentVersion &&
                change.PreviousCount == snapshot.Count && change.Count == count;
        }

        /// <summary>完整变更路径按条目实例淘汰候选测量，校验成功后再提交缓存与布局。</summary>
        private void ApplyChangedSnapshot(ListChangeSet<VirtualListItem> change, ref ChangeContext context)
        {
            var version = context.Source.Version;
            if (!IsCurrentChange(context))
            {
                return;
            }

            var next = ReadSnapshot(context.Source);
            if (!IsCurrentChange(context))
            {
                return;
            }

            var currentVersion = context.Source.Version;
            if (!IsCurrentChange(context))
            {
                return;
            }

            if (version != currentVersion)
            {
                throw new InvalidOperationException("虚拟列表来源在读取快照期间发生静默变更，请重新发布集合通知。");
            }

            if (!measureItemHeights)
            {
                var offsets = PrepareSnapshot(next);
                if (IsCurrentChange(context))
                {
                    context.PreparedVersion = version;
                    context.HasPreparedVersion = true;
                    ApplyPreparedSnapshot(next, offsets, version);
                }

                return;
            }

            // 缺失通知、版本断档或错误恢复时无法确定哪些模型已变化，统一重新测量。
            var nextMeasurements = CanApplyIncrementally(change, context)
                ? new Dictionary<VirtualListItem, float>(measuredItems)
                : new Dictionary<VirtualListItem, float>();
            if (!IsCurrentChange(context))
            {
                return;
            }

            if (nextMeasurements.Count != 0)
            {
                foreach (var entry in change.Changes)
                {
                    if (entry.Kind == ListChangeKind.Reset)
                    {
                        nextMeasurements.Clear();
                        break;
                    }

                    if (entry.Kind != ListChangeKind.Update && entry.Kind != ListChangeKind.Replace &&
                        entry.Kind != ListChangeKind.Add)
                    {
                        continue;
                    }

                    // 批次索引对应中间状态；更新后又移动的条目仍按实例使旧测量失效。
                    foreach (var item in entry.CurrentItems)
                    {
                        if (item != null)
                        {
                            nextMeasurements.Remove(item);
                            if (!IsCurrentChange(context))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            var nextOffsets = PrepareSnapshot(next, nextMeasurements);
            if (!IsCurrentChange(context))
            {
                return;
            }

            var preparedVersion = context.Source.Version;
            if (!IsCurrentChange(context))
            {
                return;
            }

            if (preparedVersion != version)
            {
                throw new InvalidOperationException("虚拟列表来源在准备测量期间发生静默变更，请重新发布集合通知。");
            }

            context.PreparedVersion = version;
            context.HasPreparedVersion = true;
            ApplyPreparedSnapshot(next, nextOffsets, version, nextMeasurements);
        }

        private bool TryUpdateItem(ListChangeSet<VirtualListItem> change, ref ChangeContext context)
        {
            if (!CanApplyIncrementally(change, context) || change.Count != snapshot.Count || change.Changes.Count != 1)
            {
                return false;
            }

            var entry = change.Changes[0];
            if ((entry.Kind != ListChangeKind.Update && entry.Kind != ListChangeKind.Replace) || entry.PreviousItems.Count != 1 || entry.CurrentItems.Count != 1 || entry.Index < 0 || entry.Index >= snapshot.Count)
            {
                return false;
            }

            var previous = snapshot[entry.Index];
            var current = entry.CurrentItems[0];
            if (current == null || !ReferenceEquals(previous, entry.PreviousItems[0]) || !ReferenceEquals(current, context.Source[entry.Index]) || !Equals(previous.Key, current.Key) || previous.Height != current.Height)
            {
                return false;
            }

            var currentVersion = context.Source.Version;
            if (!IsCurrentChange(context) || currentVersion != change.Version)
            {
                return false;
            }

            if (initialized)
            {
                ResolveItemTemplate(current.TemplateKey);
            }
            if (measureItemHeights && !current.Height.HasValue)
            {
                // 原位模型更新也可能改变高度，交由完整变更路径统一淘汰旧测量。
                return false;
            }

            if (!IsCurrentChange(context))
            {
                return false;
            }

            // 保留选择和滚动锚点，仅此键借用的 ViewModel 可能变化。
            context.PreparedVersion = change.Version;
            context.HasPreparedVersion = true;
            snapshot[entry.Index] = current;
            snapshotVersion = change.Version;
            ++sourceRevision;
            ++revealSourceRevision;
            if (entry.Index >= first && entry.Index < last)
            {
                first = last = -1;
            }

            RequestRefresh(recover: true);
            return true;
        }

        private bool TryAppend(ListChangeSet<VirtualListItem> change, ref ChangeContext context)
        {
            if (!CanApplyIncrementally(change, context))
            {
                return false;
            }

            var start = snapshot.Count;
            long expectedCount = start;
            foreach (var entry in change.Changes)
            {
                if (entry.Kind != ListChangeKind.Add || entry.Index != expectedCount || entry.PreviousItems.Count != 0)
                {
                    return false;
                }

                expectedCount += entry.CurrentItems.Count;
            }

            if (expectedCount != change.Count || expectedCount == start)
            {
                return false;
            }

            // 变高行必须重建行索引，避免为注定回退的追加复制整份键索引。
            if (measureItemHeights || rowIndex != null)
            {
                return false;
            }

            foreach (var entry in change.Changes)
            {
                foreach (var item in entry.CurrentItems)
                {
                    if (item != null && item.Height.HasValue)
                    {
                        return false;
                    }
                }
            }

            var added = new List<VirtualListItem>();
            var intrinsicKeys = intrinsicKeyIndex;
            foreach (var entry in change.Changes)
            {
                foreach (var item in entry.CurrentItems)
                {
                    if (item == null)
                    {
                        throw new InvalidOperationException("Virtual list requires non-null items with unique stable keys.");
                    }

                    intrinsicKeys &= IsIntrinsicKey(item.Key);
                    added.Add(item);
                }
            }

            if (added.Count == 0 || (long)start + added.Count != change.Count)
            {
                return false;
            }

            // 内建值键不会进入项目回调，可在最终校验后直接提交；自定义键保持候选索引隔离。
            Dictionary<object, int> nextIndices = null;
            if (intrinsicKeys)
            {
                var newKeys = new HashSet<object>();
                foreach (var item in added)
                {
                    if (keyIndices.ContainsKey(item.Key) || !newKeys.Add(item.Key))
                    {
                        throw new InvalidOperationException("Virtual list requires non-null items with unique stable keys.");
                    }
                }
            }
            else
            {
                try
                {
                    nextIndices = new Dictionary<object, int>(keyIndices);
                }
                catch when (!IsCurrentChange(context))
                {
                    return false;
                }

                if (!IsCurrentChange(context))
                {
                    return false;
                }

                for (var i = 0; i < added.Count; ++i)
                {
                    var addedToIndex = nextIndices.TryAdd(added[i].Key, start + i);
                    if (!IsCurrentChange(context))
                    {
                        return false;
                    }

                    if (!addedToIndex)
                    {
                        throw new InvalidOperationException("Virtual list requires non-null items with unique stable keys.");
                    }
                }
            }

            for (var i = 0; i < added.Count; ++i)
            {
                var current = context.Source[start + i];
                if (!IsCurrentChange(context) || !ReferenceEquals(current, added[i]))
                {
                    return false;
                }
            }

            var currentVersion = context.Source.Version;
            if (!IsCurrentChange(context) || currentVersion != change.Version)
            {
                return false;
            }

            if (initialized)
            {
                ValidateItemTemplates(added);
            }

            if (!IsCurrentChange(context))
            {
                return false;
            }

            var finalVersion = context.Source.Version;
            if (!IsCurrentChange(context) || finalVersion != change.Version)
            {
                return false;
            }

            Layout.ContentHeight(change.Count);
            // 追加时已有索引、选择和首个可见键保持不变。
            context.PreparedVersion = change.Version;
            context.HasPreparedVersion = true;
            if (intrinsicKeys)
            {
                snapshot.Capacity = Math.Max(snapshot.Capacity, change.Count);
                keyIndices.EnsureCapacity(change.Count);
                for (var i = 0; i < added.Count; ++i)
                {
                    if (!keyIndices.TryAdd(added[i].Key, start + i))
                    {
                        throw new InvalidOperationException("Virtual list requires non-null items with unique stable keys.");
                    }
                }
            }

            snapshot.AddRange(added);
            if (!intrinsicKeys)
            {
                keyIndices = nextIndices;
                intrinsicKeyIndex = false;
            }

            snapshotVersion = change.Version;
            ++sourceRevision;
            ++revealSourceRevision;
            UpdateContentHeight();
            // 追加尾部在当前范围之外时，保留当前实例化范围。
            // 进行中的刷新会观察到脏标记，并在发布前重新计算范围。
            RequestRefresh(recover: true);
            return true;
        }

        private static bool IsIntrinsicKey(object key) => key is string || key is int || key is long || key is Guid;

        private ChangeContext CaptureChangeContext() => new ChangeContext(items, itemsSubscription,
            itemsAssignmentVersion, lifetime, sourceRevision);

        private bool IsCurrentSourceIdentity(ChangeContext context) => IsAlive && context.Source != null &&
            ReferenceEquals(items, context.Source) && ReferenceEquals(itemsSubscription, context.Subscription) &&
            itemsAssignmentVersion == context.Assignment && ReferenceEquals(lifetime, context.Activation) &&
            context.Activation != null && !context.Activation.IsEnded && scope != null && scope.IsActive;

        private bool IsCurrentChange(ChangeContext context) => IsCurrentSourceIdentity(context) &&
            sourceRevision == context.Revision;

        // 提交后代际前进一次；只有版本仍是本次候选时，后续布局故障才归本次通知。
        private bool IsCurrentChangeFailure(ChangeContext context) => IsCurrentChange(context) ||
            (IsCurrentSourceIdentity(context) && context.HasPreparedVersion &&
            sourceRevision == context.Revision + 1 && snapshotVersion == context.PreparedVersion);

        private struct ChangeContext
        {
            public readonly IReadOnlyObservableList<VirtualListItem> Source;
            public readonly object Subscription;
            public readonly long Assignment;
            public readonly Lifetime Activation;
            public readonly long Revision;
            public long PreparedVersion;
            public bool HasPreparedVersion;

            public ChangeContext(IReadOnlyObservableList<VirtualListItem> source, object subscription,
                long assignment, Lifetime activation, long revision)
            {
                Source = source;
                Subscription = subscription;
                Assignment = assignment;
                Activation = activation;
                Revision = revision;
                PreparedVersion = 0;
                HasPreparedVersion = false;
            }
        }
    }
}
