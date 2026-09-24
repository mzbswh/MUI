using System;
using System.Collections.Generic;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private bool CanApplyIncrementally(ListChangeSet<VirtualListItem> change) => items != null && change != null && Error == null && change.PreviousVersion == snapshotVersion && change.Version == items.Version && change.PreviousCount == snapshot.Count && change.Count == items.Count;

        /// <summary>完整变更路径按稳定键淘汰候选测量，校验成功后再提交缓存与布局。</summary>
        private void ApplyChangedSnapshot(ListChangeSet<VirtualListItem> change)
        {
            var next = ReadSnapshot(items);
            if (!measureItemHeights)
            {
                ApplySnapshot(next);
                return;
            }

            // 缺失通知、版本断档或错误恢复时无法确定哪些模型已变化，统一重新测量。
            var nextMeasurements = CanApplyIncrementally(change)
                ? new Dictionary<object, MeasuredItem>(measuredItems)
                : new Dictionary<object, MeasuredItem>();
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
            measuredItems.Clear();
            foreach (var measurement in nextMeasurements)
            {
                measuredItems.Add(measurement.Key, measurement.Value);
            }

            ApplyPreparedSnapshot(next, nextOffsets);
        }

        private bool TryUpdateItem(ListChangeSet<VirtualListItem> change)
        {
            if (!CanApplyIncrementally(change) || change.Count != snapshot.Count || change.Changes.Count != 1)
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
            if (current == null || !ReferenceEquals(previous, entry.PreviousItems[0]) || !ReferenceEquals(current, items[entry.Index]) || !Equals(previous.Key, current.Key) || previous.Height != current.Height)
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

            // 保留选择和滚动锚点，仅此键借用的 ViewModel 可能变化。
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

        private bool TryAppend(ListChangeSet<VirtualListItem> change)
        {
            if (!CanApplyIncrementally(change))
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
                if (!ReferenceEquals(items[snapshot.Count + i], added[i]))
                {
                    return false;
                }
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

            Layout.ContentHeight(change.Count);
            // 追加时已有索引、选择和首个可见键保持不变。
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
    }
}
