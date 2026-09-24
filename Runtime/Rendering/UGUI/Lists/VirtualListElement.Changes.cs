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

        /// <summary>完整变更路径按稳定键淘汰候选测量，校验成功后再提交缓存与布局。</summary>
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
                ? new Dictionary<object, MeasuredItem>(measuredItems)
                : new Dictionary<object, MeasuredItem>();
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

                    // 批次索引对应中间状态；更新后又移动的条目仍按键使旧测量失效。
                    foreach (var item in entry.CurrentItems)
                    {
                        if (item != null)
                        {
                            nextMeasurements.Remove(item.Key);
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

            measuredItems.Clear();
            foreach (var measurement in nextMeasurements)
            {
                measuredItems.Add(measurement.Key, measurement.Value);
            }

            context.PreparedVersion = version;
            context.HasPreparedVersion = true;
            ApplyPreparedSnapshot(next, nextOffsets, version);
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

            long expectedCount = snapshot.Count;
            foreach (var entry in change.Changes)
            {
                if (entry.Kind != ListChangeKind.Add || entry.Index != expectedCount || entry.PreviousItems.Count != 0)
                {
                    return false;
                }

                expectedCount += entry.CurrentItems.Count;
            }

            if (expectedCount != change.Count || expectedCount == snapshot.Count)
            {
                return false;
            }

            var added = new List<VirtualListItem>();
            var addedKeys = new HashSet<object>();
            foreach (var entry in change.Changes)
            {
                foreach (var item in entry.CurrentItems)
                {
                    if (item == null || keyIndices.ContainsKey(item.Key) || !addedKeys.Add(item.Key))
                    {
                        throw new InvalidOperationException("Virtual list requires non-null items with unique stable keys.");
                    }

                    added.Add(item);
                }
            }

            if (added.Count == 0 || (long)snapshot.Count + added.Count != change.Count)
            {
                return false;
            }

            for (var i = 0; i < added.Count; ++i)
            {
                if (!ReferenceEquals(context.Source[snapshot.Count + i], added[i]))
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
            if (measureItemHeights || rowIndex != null || added.Exists(item => item.Height.HasValue))
            {
                // 变高行追加需重建行位置索引，由完整快照路径统一保持滚动锚点。
                // 空列表尚无行索引，首次追加也必须为后续高度测量创建索引。
                return false;
            }

            if (!IsCurrentChange(context))
            {
                return false;
            }

            Layout.ContentHeight(change.Count);
            // 追加时已有索引、选择和首个可见键保持不变。
            context.PreparedVersion = change.Version;
            context.HasPreparedVersion = true;
            var start = snapshot.Count;
            snapshot.AddRange(added);
            for (var i = 0; i < added.Count; ++i)
            {
                keyIndices.Add(added[i].Key, start + i);
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
