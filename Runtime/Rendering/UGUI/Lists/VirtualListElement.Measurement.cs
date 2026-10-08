using System;
using System.Collections.Generic;
using System.Threading;
using MUI.ChildViews;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("measureItemHeights")]
        private bool measureItemExtents;
        [SerializeField, Min(1)]
        private int measurementsPerFrame = 4;
        // 工作集按条目实例查询；来源变更准备时才按稳定键、显式内容版本和模板迁移缓存。
        private Dictionary<VirtualListItem, float> measuredItems = new Dictionary<VirtualListItem, float>();
        private float measurementCrossExtent = -1;
        private bool measuring;

        // 多列规则 Grid 使用配置的统一尺寸，逐项显式尺寸与自动测量仅用于单列列表。
        private bool UsesMeasuredExtents => measureItemExtents && !IsUniformGrid;

        /// <summary>在初始化前启用布局滚动轴首选尺寸测量；显式条目尺寸始终优先。</summary>
        public void ConfigureSizeMeasurement(bool enabled, int perFrame = 4)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Configure size measurement before initialization.");
            }

            if (perFrame < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(perFrame));
            }

            measureItemExtents = enabled;
            measurementsPerFrame = perFrame;
        }

        /// <summary>字体、语言、字号或模板布局变化后调用；清除测量结果并分帧重测可见条目。</summary>
        public void InvalidateSizeMeasurements()
        {
            RequireListAlive();
            RequireMeasurementMutation();
            if (initialized && Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("Size invalidation must run on its UI thread.");
            }

            if (initialized && UsesMeasuredExtents)
            {
                ResetMeasuredLayout(measurementCrossExtent);
            }
            else
            {
                measuredItems.Clear();
            }
        }

        private float EffectiveExtent(VirtualListItem item) =>
            IsUniformGrid ? estimatedItemExtent : ListItemExtent(item);

        private float ListItemExtent(VirtualListItem item,
            IReadOnlyDictionary<VirtualListItem, float> candidateMeasurements = null)
        {
            if (item.Extent.HasValue)
            {
                return item.Extent.Value;
            }

            var measurements = candidateMeasurements ?? measuredItems;
            if (measureItemExtents && measurements.TryGetValue(item, out var saved))
            {
                return saved;
            }

            return estimatedItemExtent;
        }

        private static Dictionary<VirtualListItem, float> PrepareMeasurements(
            List<VirtualListItem> next, Dictionary<VirtualListItem, float> previous, bool discard)
        {
            var retained = new Dictionary<VirtualListItem, float>();
            if (discard)
            {
                return retained;
            }

            var versioned = new Dictionary<object, KeyValuePair<VirtualListItem, float>>();
            foreach (var entry in previous)
            {
                if (entry.Key.ContentVersion.HasValue)
                {
                    versioned.Add(entry.Key.Key, entry);
                }
            }

            foreach (var item in next)
            {
                if (previous.TryGetValue(item, out var extent))
                {
                    retained.Add(item, extent);
                }
                else if (item.ContentVersion.HasValue && versioned.TryGetValue(item.Key, out var saved) &&
                    saved.Key.ContentVersion == item.ContentVersion &&
                    string.Equals(saved.Key.TemplateKey, item.TemplateKey, StringComparison.Ordinal))
                {
                    retained.Add(item, saved.Value);
                }
            }

            return retained;
        }

        /// <summary>图片、文字或局部布局变化后仅使指定稳定键的测量失效，保持当前阅读锚点。</summary>
        public bool InvalidateItemSize(object key)
        {
            RequireListAlive();
            RequireMeasurementMutation();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (initialized && Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("Size invalidation must run on its UI thread.");
            }

            if (!UsesMeasuredExtents)
            {
                return false;
            }

            var revision = sourceRevision;
            var activation = lifetime;
            var found = keyIndices.TryGetValue(key, out var index);
            if (!IsAlive || revision != sourceRevision || !ReferenceEquals(lifetime, activation) || !found)
            {
                return false;
            }

            var item = snapshot[index];
            if (!measuredItems.ContainsKey(item))
            {
                return false;
            }

            var follow = ShouldFollowEnd();
            var anchor = FirstVisibleIndex;
            var offset = anchor < 0 ? 0 : ScrollOffset - Layout.OffsetForIndex(anchor);
            var candidate = new Dictionary<VirtualListItem, float>(measuredItems);
            candidate.Remove(item);
            var offsets = BuildRowIndex(snapshot, columns, candidate);
            CreateLayout(columns, overscan, offsets).ContentExtent(snapshot.Count);
            measuredItems = candidate;
            rowIndex = offsets;
            RefreshMeasuredGeometry(anchor, offset, follow);
            return true;
        }

        private void RequireMeasurementMutation()
        {
            if (preparedRebind != null)
            {
                throw new InvalidOperationException("Cannot invalidate measurements during a prepared binding commit.");
            }
        }

        private void ResetMeasuredLayout(float crossExtent)
        {
            var follow = ShouldFollowEnd();
            var anchor = FirstVisibleIndex;
            var offset = anchor < 0 ? 0 : ScrollOffset - Layout.OffsetForIndex(anchor);
            // 用空候选计算估算布局；总尺寸校验失败时保留已提交的测量与交叉轴尺寸。
            var nextOffsets = BuildRowIndex(snapshot, columns, new Dictionary<VirtualListItem, float>());
            CreateLayout(columns, overscan, nextOffsets).ContentExtent(snapshot.Count);
            measuredItems.Clear();
            measurementCrossExtent = crossExtent;
            rowIndex = nextOffsets;
            RefreshMeasuredGeometry(anchor, offset, follow);
        }

        private void RefreshMeasuredGeometry(int anchor, float offset, bool follow)
        {
            var revision = ++sourceRevision;
            first = last = -1;
            UpdateContentExtent();
            if (!IsAlive || revision != sourceRevision)
            {
                return;
            }

            if (anchor >= 0 && activeReveal == null)
            {
                SetReadingOffset(follow ? EndOffset : Layout.OffsetForIndex(anchor) + Math.Min(offset, Layout.RowExtentForIndex(anchor)));
            }

            if (IsAlive && revision == sourceRevision)
            {
                RequestRefresh();
            }
        }

        private void ApplyMeasuredRows(HashSet<int> rows, Dictionary<VirtualListItem, float> measuredBatch,
            ChildViewScope owner, long revision)
        {
            if (measuredBatch.Count == 0)
            {
                return;
            }

            var previousMeasurements = measuredItems;
            var previousRows = rowIndex;
            bool IsCurrent() => IsMeasurementSourceCurrent(owner, revision) &&
                ReferenceEquals(measuredItems, previousMeasurements) && ReferenceEquals(rowIndex, previousRows);

            var follow = ShouldFollowEnd();
            var anchor = FirstVisibleIndex;
            var offset = anchor < 0 ? 0 : ScrollOffset - Layout.OffsetForIndex(anchor);
            var changes = new List<KeyValuePair<int, float>>(rows.Count);
            var total = rowIndex == null ? 0 : rowIndex.TotalExtent;
            foreach (var row in rows)
            {
                var extent = 0f;
                var start = row * columns;
                var end = (int)Math.Min(snapshot.Count, (long)start + columns);
                for (var index = start; index < end; ++index)
                {
                    var item = snapshot[index];
                    var itemExtent = measuredBatch.TryGetValue(item, out var measured)
                        ? measured : EffectiveExtent(item);
                    if (!IsCurrent())
                    {
                        return;
                    }

                    extent = Math.Max(extent, itemExtent);
                }

                total += (double)extent - rowIndex.Extent(row);
                changes.Add(new KeyValuePair<int, float>(row, extent));
            }

            VirtualRowIndex.ValidateTotal(total + (double)Math.Max(0, rowIndex.Count - 1) * MainSpacing +
                LeadingPadding + TrailingPadding);
            if (!IsCurrent())
            {
                return;
            }

            // 缓存按条目实例索引，提交期间不会执行项目键回调。
            foreach (var measured in measuredBatch)
            {
                measuredItems[measured.Key] = measured.Value;
            }

            if (changes.Count == 0)
            {
                return;
            }

            // 先缩小再增大，保证最终总尺寸合法时中间步骤不会暂时超出几何上限。
            foreach (var change in changes)
            {
                if (change.Value < rowIndex.Extent(change.Key))
                {
                    rowIndex.SetExtent(change.Key, change.Value);
                }
            }

            foreach (var change in changes)
            {
                if (change.Value > rowIndex.Extent(change.Key))
                {
                    rowIndex.SetExtent(change.Key, change.Value);
                }
            }

            RefreshMeasuredGeometry(anchor, offset, follow);
        }

        private void MeasureVisibleItems()
        {
            if (!UsesMeasuredExtents || measuring || running || IsVisualRetentionActive)
            {
                return;
            }

            measuring = true;
            try
            {
                var crossExtent = ItemCrossExtent;
                if (crossExtent <= 0 || float.IsNaN(crossExtent) || float.IsInfinity(crossExtent))
                {
                    return;
                }

                if (measurementCrossExtent != crossExtent)
                {
                    ResetMeasuredLayout(crossExtent);
                    return;
                }

                // 稳定视口通常已经完成测量，不为每个空闲帧创建批次集合和单元快照。
                var hasPendingMeasurement = false;
                var revision = sourceRevision;
                var owner = scope;
                for (var i = 0; i < cells.Count; i++)
                {
                    var pending = TryGetUnmeasuredItem(cells[i], out _, out _);
                    if (!IsMeasurementSourceCurrent(owner, revision))
                    {
                        return;
                    }

                    if (pending)
                    {
                        hasPendingMeasurement = true;
                        break;
                    }
                }

                if (!hasPendingMeasurement)
                {
                    return;
                }

                var changedRows = new HashSet<int>();
                var measuredBatch = new Dictionary<VirtualListItem, float>();
                var measuredCells = new List<(Cell Cell, VirtualListItem Item, int Index, RectTransform Root)>();
                List<(VirtualListItem Item, int Index)> invalidItems = null;
                var remaining = measurementsPerFrame;
                foreach (var cell in cells.ToArray())
                {
                    if (remaining == 0)
                    {
                        break;
                    }

                    var pending = TryGetUnmeasuredItem(cell, out var item, out var index);
                    if (!IsMeasurementSourceCurrent(owner, revision))
                    {
                        return;
                    }

                    if (!pending)
                    {
                        continue;
                    }

                    --remaining;
                    // 只重建已绑定的可见单元，不创建屏外测量实例，也不执行全 Canvas 强制刷新。
                    var measurementRoot = cell.MeasurementRoot;
                    float extent;
                    try
                    {
                        using var sample = nativeMeasurementMarker.Auto();
                        RecordMeasurementAttempt();
                        ImmediateLayout.Rebuild(measurementRoot);
                        if (!IsCurrentMeasurement(owner, revision, crossExtent, cell, item, index, measurementRoot))
                        {
                            return;
                        }

                        // ILayoutElement 的 getter 也可能进入项目代码，读取后必须再次确认归属。
                        extent = IsHorizontal ? LayoutUtility.GetPreferredWidth(measurementRoot) : LayoutUtility.GetPreferredHeight(measurementRoot);
                    }
                    catch when (!IsCurrentMeasurement(owner, revision, crossExtent, cell, item, index, measurementRoot))
                    {
                        // 已关闭或被新来源替代的测量不再把错误发布到新一轮列表状态。
                        return;
                    }

                    if (!IsCurrentMeasurement(owner, revision, crossExtent, cell, item, index, measurementRoot))
                    {
                        return;
                    }

                    if (extent <= 0 || float.IsNaN(extent) || float.IsInfinity(extent))
                    {
                        // 缓存有效估算值，直到内容或布局显式失效，避免每帧重复测量同一个坏结果。
                        extent = estimatedItemExtent;
                        if (invalidItems == null)
                        {
                            invalidItems = new List<(VirtualListItem Item, int Index)>();
                        }

                        invalidItems.Add((item, index));
                    }

                    measuredBatch[item] = extent;
                    if (!IsCurrentMeasurement(owner, revision, crossExtent, cell, item, index, measurementRoot))
                    {
                        return;
                    }

                    measuredCells.Add((cell, item, index, measurementRoot));
                    if (extent != estimatedItemExtent)
                    {
                        changedRows.Add(index / columns);
                    }
                }

                // 后续条目的布局回调可能回收此前条目；整批复核通过后才发布测量缓存。
                foreach (var candidate in measuredCells)
                {
                    if (!IsCurrentMeasurement(owner, revision, crossExtent, candidate.Cell,
                        candidate.Item, candidate.Index, candidate.Root))
                    {
                        return;
                    }
                }

                // 总尺寸也通过校验后，再统一发布测量缓存与行位置。
                ApplyMeasuredRows(changedRows, measuredBatch, owner, revision);
                if (invalidItems != null)
                {
                    foreach (var invalid in invalidItems)
                    {
                        if (!IsAlive || owner == null || !ReferenceEquals(scope, owner) || !owner.IsActive ||
                            invalid.Index >= snapshot.Count || !ReferenceEquals(snapshot[invalid.Index], invalid.Item) ||
                            !measuredItems.ContainsKey(invalid.Item))
                        {
                            continue;
                        }

                        UIErrors.Report(new InvalidOperationException(
                            $"Virtual list item at index {invalid.Index} must expose a finite positive preferred size."));
                    }
                }
            }
            finally
            {
                measuring = false;
            }
        }

        private bool TryGetUnmeasuredItem(Cell cell, out VirtualListItem item, out int index)
        {
            item = null;
            index = -1;
            var owner = scope;
            var revision = sourceRevision;
            var key = cell.Key;
            if (key == null || cell.Root == null || cell.MeasurementRoot == null ||
                !cell.Root.gameObject.activeInHierarchy || !cell.MeasurementRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            var found = keyIndices.TryGetValue(key, out index);
            if (!IsMeasurementSourceCurrent(owner, revision) || !ReferenceEquals(cell.Key, key) ||
                !found || index < 0 || index >= snapshot.Count)
            {
                index = -1;
                return false;
            }

            item = snapshot[index];
            var unmeasured = !item.Extent.HasValue && !measuredItems.ContainsKey(item);
            if (!IsMeasurementSourceCurrent(owner, revision) || !ReferenceEquals(cell.Key, key) ||
                index >= snapshot.Count || !ReferenceEquals(snapshot[index], item) || cell.Element == null)
            {
                item = null;
                index = -1;
                return false;
            }

            return unmeasured &&
                ReferenceEquals(cell.Element.DisplayedViewModel, item.ViewModel);
        }

        private bool IsMeasurementSourceCurrent(ChildViewScope owner, long revision) =>
            IsAlive && ReferenceEquals(scope, owner) && owner != null && owner.IsActive &&
            revision == sourceRevision && !dirty && Error == null;

        private bool IsCurrentMeasurement(ChildViewScope owner, long revision, float crossExtent, Cell cell,
                    VirtualListItem item, int index, RectTransform measurementRoot)
        {
            if (!IsMeasurementSourceCurrent(owner, revision) || IsVisualRetentionActive ||
                scrollRect == null || scrollRect.content == null ||
                measurementCrossExtent != crossExtent || ItemCrossExtent != crossExtent ||
                measurementRoot == null || !measurementRoot.gameObject.activeInHierarchy ||
                cell.Root == null || !cell.Root.gameObject.activeInHierarchy ||
                cell.MeasurementRoot != measurementRoot || cell.Element == null || cell.Key == null ||
                index < 0 || index >= snapshot.Count || !ReferenceEquals(snapshot[index], item))
            {
                return false;
            }

            var key = cell.Key;
            var found = keyIndices.TryGetValue(key, out var currentIndex);
            return found && IsMeasurementSourceCurrent(owner, revision) && ReferenceEquals(cell.Key, key) &&
                index < snapshot.Count && ReferenceEquals(snapshot[index], item) && currentIndex == index &&
                cell.Element != null && ReferenceEquals(cell.Element.DisplayedViewModel, item.ViewModel);
        }
    }
}
