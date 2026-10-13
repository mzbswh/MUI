using System;

namespace MUI.UGUI
{
    /// <summary>沿滚动轴查询固定或可变尺寸行；可变尺寸行使用预建位置索引，滚动查询不扫描全量条目。</summary>
    internal readonly struct VirtualGridLayout
    {
        private readonly VirtualRowIndex rowIndex;
        private readonly float spacing;
        private readonly float leadingPadding;
        private readonly float trailingPadding;

        public VirtualGridLayout(float rowExtent, int columns, int overscan, VirtualRowIndex rowIndex = null,
            float spacing = 0, float leadingPadding = 0, float trailingPadding = 0)
        {
            if (rowExtent <= 0 || float.IsNaN(rowExtent) || float.IsInfinity(rowExtent))
            {
                throw new ArgumentOutOfRangeException(nameof(rowExtent));
            }

            if (columns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            if (overscan < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(overscan));
            }

            RequireNonnegative(spacing);
            RequireNonnegative(leadingPadding);
            RequireNonnegative(trailingPadding);
            this.spacing = spacing;
            this.leadingPadding = leadingPadding;
            this.trailingPadding = trailingPadding;
            this.rowIndex = rowIndex;
            RowExtent = rowExtent;
            Columns = columns;
            Overscan = overscan;
        }

        public float RowExtent
        {
            get;
        }

        public int Columns
        {
            get;
        }

        public int Overscan
        {
            get;
        }

        /// <summary>几何查询共用行数校验，防止旧的尺寸索引被用于新的条目数量。</summary>
        private int Rows(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var rows = count == 0 ? 0 : (count - 1) / Columns + 1;
            if (rowIndex != null && rowIndex.Count != rows)
            {
                throw new InvalidOperationException("Virtual row index does not match the item count.");
            }

            return rows;
        }

        public float ContentExtent(int count)
        {
            var rows = Rows(count);
            var extent = rowIndex == null ? (double)rows * RowExtent : rowIndex.TotalExtent;
            extent += (double)Math.Max(0, rows - 1) * spacing + leadingPadding + trailingPadding;
            if (extent > float.MaxValue)
            {
                throw new InvalidOperationException("Virtual content exceeds supported geometry.");
            }

            return (float)extent;
        }

        private double RowOffset(int row) => leadingPadding + (double)row * spacing +
            (rowIndex == null ? (double)row * RowExtent : rowIndex.Offset(row));

        public float OffsetForIndex(int index) => (float)RowOffset(index / Columns);

        private int FirstIntersectingRow(int rows, double offset)
        {
            var localOffset = offset - leadingPadding;
            var row = (int)Math.Min(rows, Math.Max(0, rowIndex == null
                ? Math.Floor(localOffset / ((double)RowExtent + spacing))
                : rowIndex.Bound(localOffset, true, spacing) - 1));
            // 起点落在间距内时，前一行不与视口相交。
            if (row < rows && RowOffset(row) + (rowIndex == null ? RowExtent : rowIndex.Extent(row)) <= offset)
            {
                ++row;
            }
            return row;
        }

        public float RowExtentForIndex(int index) => rowIndex == null ? RowExtent :
                    rowIndex.Extent(index / Columns);

        public int FirstVisible(int count, float offset)
        {
            RequireFinite(offset);
            var rows = Rows(count);
            if (count == 0)
            {
                return -1;
            }

            return Math.Min(rows - 1, FirstIntersectingRow(rows, offset)) * Columns;
        }

        public void GetRange(int count, float offset, float viewportExtent, out int start, out int end)
        {
            RequireFinite(offset);
            RequireFinite(viewportExtent);
            var rows = Rows(count);
            if (viewportExtent <= 0 || rows == 0)
            {
                start = end = 0;
                return;
            }

            var firstRow = FirstIntersectingRow(rows, offset);
            var endOffset = (double)offset + viewportExtent - leadingPadding;
            var lastRow = rowIndex == null
                ? Math.Ceiling(endOffset / ((double)RowExtent + spacing))
                : rowIndex.Bound(endOffset, false, spacing);
            var first = Math.Min(rows, Math.Max(0, (long)firstRow - Overscan));
            var last = Math.Min(rows, Math.Max(first, lastRow + Overscan));
            start = (int)Math.Min(count, first * Columns);
            end = (int)Math.Min(count, last * Columns);
        }

        private static void RequireNonnegative(float value)
        {
            RequireFinite(value);
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static void RequireFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException("Virtual viewport geometry must be finite.");
            }
        }
    }
}
