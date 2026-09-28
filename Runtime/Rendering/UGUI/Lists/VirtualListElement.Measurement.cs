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
        [SerializeField]
        private bool measureItemHeights;
        [SerializeField, Min(1)]
        private int measurementsPerFrame = 4;
        // 高度仅随同一条目实例复用，避免缓存查询执行项目键的比较逻辑。
        private Dictionary<VirtualListItem, float> measuredItems = new Dictionary<VirtualListItem, float>();
        private float measurementWidth = -1;
        private bool measuring;

        /// <summary>在初始化前启用布局首选高度测量；显式条目高度始终优先。</summary>
        public void ConfigureHeightMeasurement(bool enabled, int perFrame = 4)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Configure height measurement before initialization.");
            }

            if (perFrame < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(perFrame));
            }

            measureItemHeights = enabled;
            measurementsPerFrame = perFrame;
        }

        /// <summary>字体、语言、字号或模板布局变化后调用；清除测量结果并分帧重测可见条目。</summary>
        public void InvalidateHeightMeasurements()
        {
            RequireListAlive();
            if (initialized && Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("Height invalidation must run on its UI thread.");
            }

            if (initialized && measureItemHeights)
            {
                ResetMeasuredLayout(measurementWidth);
            }
            else
            {
                measuredItems.Clear();
            }
        }

        private float EffectiveHeight(VirtualListItem item,
                    IReadOnlyDictionary<VirtualListItem, float> candidateMeasurements = null)
        {
            if (item.Height.HasValue)
            {
                return item.Height.Value;
            }

            var measurements = candidateMeasurements ?? measuredItems;
            if (measureItemHeights && measurements.TryGetValue(item, out var saved))
            {
                return saved;
            }

            return rowHeight;
        }

        private static Dictionary<VirtualListItem, float> PrepareMeasurements(
            List<VirtualListItem> next, Dictionary<VirtualListItem, float> previous, bool discard)
        {
            var retained = new Dictionary<VirtualListItem, float>();
            if (discard)
            {
                return retained;
            }

            var current = new HashSet<VirtualListItem>(next);
            foreach (var entry in previous)
            {
                if (current.Contains(entry.Key))
                {
                    retained.Add(entry.Key, entry.Value);
                }
            }

            return retained;
        }

        private void ResetMeasuredLayout(float width)
        {
            var anchor = FirstVisibleIndex;
            var offset = anchor < 0 ? 0 : scrollRect.content.anchoredPosition.y - Layout.OffsetForIndex(anchor);
            // 用空候选计算估算布局；总高度校验失败时保留已提交的测量与宽度。
            var nextOffsets = BuildRowIndex(snapshot, columns, new Dictionary<VirtualListItem, float>());
            measuredItems.Clear();
            measurementWidth = width;
            rowIndex = nextOffsets;
            RefreshMeasuredGeometry(anchor, offset);
        }

        private void RefreshMeasuredGeometry(int anchor, float offset)
        {
            var revision = ++sourceRevision;
            first = last = -1;
            UpdateContentHeight();
            if (!IsAlive || revision != sourceRevision)
            {
                return;
            }

            if (anchor >= 0)
            {
                SetOffset(Layout.OffsetForIndex(anchor) + Math.Min(offset, Layout.RowHeightForIndex(anchor)));
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

            var anchor = FirstVisibleIndex;
            var offset = anchor < 0 ? 0 : scrollRect.content.anchoredPosition.y - Layout.OffsetForIndex(anchor);
            var changes = new List<KeyValuePair<int, float>>(rows.Count);
            var total = rowIndex == null ? 0 : rowIndex.TotalHeight;
            foreach (var row in rows)
            {
                var height = 0f;
                var start = row * columns;
                var end = (int)Math.Min(snapshot.Count, (long)start + columns);
                for (var index = start; index < end; ++index)
                {
                    var item = snapshot[index];
                    var itemHeight = measuredBatch.TryGetValue(item, out var measured)
                        ? measured : EffectiveHeight(item);
                    if (!IsCurrent())
                    {
                        return;
                    }

                    height = Math.Max(height, itemHeight);
                }

                total += (double)height - rowIndex.Height(row);
                changes.Add(new KeyValuePair<int, float>(row, height));
            }

            VirtualRowIndex.ValidateTotal(total);
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

            // 先缩小再增大，保证最终总高度合法时中间步骤不会暂时超出几何上限。
            foreach (var change in changes)
            {
                if (change.Value < rowIndex.Height(change.Key))
                {
                    rowIndex.SetHeight(change.Key, change.Value);
                }
            }

            foreach (var change in changes)
            {
                if (change.Value > rowIndex.Height(change.Key))
                {
                    rowIndex.SetHeight(change.Key, change.Value);
                }
            }

            RefreshMeasuredGeometry(anchor, offset);
        }

        private void MeasureVisibleItems()
        {
            if (!measureItemHeights || measuring || running || IsVisualRetentionActive)
            {
                return;
            }

            measuring = true;
            try
            {
                var width = scrollRect.content.rect.width / columns;
                if (width <= 0 || float.IsNaN(width) || float.IsInfinity(width))
                {
                    return;
                }

                if (measurementWidth != width)
                {
                    ResetMeasuredLayout(width);
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
                    float height;
                    try
                    {
                        ImmediateLayout.Rebuild(measurementRoot);
                        if (!IsCurrentMeasurement(owner, revision, width, cell, item, index, measurementRoot))
                        {
                            return;
                        }

                        // ILayoutElement 的 getter 也可能进入项目代码，读取后必须再次确认归属。
                        height = LayoutUtility.GetPreferredHeight(measurementRoot);
                    }
                    catch when (!IsCurrentMeasurement(owner, revision, width, cell, item, index, measurementRoot))
                    {
                        // 已关闭或被新来源替代的测量不再把错误发布到新一轮列表状态。
                        return;
                    }

                    if (!IsCurrentMeasurement(owner, revision, width, cell, item, index, measurementRoot))
                    {
                        return;
                    }

                    if (height <= 0 || float.IsNaN(height) || float.IsInfinity(height))
                    {
                        throw new InvalidOperationException("Measured item root must expose a finite positive preferred height.");
                    }

                    measuredBatch[item] = height;
                    if (!IsCurrentMeasurement(owner, revision, width, cell, item, index, measurementRoot))
                    {
                        return;
                    }

                    measuredCells.Add((cell, item, index, measurementRoot));
                    if (height != rowHeight)
                    {
                        changedRows.Add(index / columns);
                    }
                }

                // 后续条目的布局回调可能回收此前条目；整批复核通过后才发布测量缓存。
                foreach (var candidate in measuredCells)
                {
                    if (!IsCurrentMeasurement(owner, revision, width, candidate.Cell,
                        candidate.Item, candidate.Index, candidate.Root))
                    {
                        return;
                    }
                }

                // 总高度也通过校验后，再统一发布测量缓存与行位置。
                ApplyMeasuredRows(changedRows, measuredBatch, owner, revision);
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
            var unmeasured = !item.Height.HasValue && !measuredItems.ContainsKey(item);
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

        private bool IsCurrentMeasurement(ChildViewScope owner, long revision, float width, Cell cell,
                    VirtualListItem item, int index, RectTransform measurementRoot)
        {
            if (!IsMeasurementSourceCurrent(owner, revision) || IsVisualRetentionActive ||
                scrollRect == null || scrollRect.content == null ||
                measurementWidth != width || scrollRect.content.rect.width / columns != width ||
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
