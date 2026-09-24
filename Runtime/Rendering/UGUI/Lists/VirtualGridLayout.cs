using System;

namespace MUI.UGUI
{
    /// <summary>固定或变高行的几何查询；变高行使用预建位置索引，滚动查询不扫描全量条目。</summary>
    internal readonly struct VirtualGridLayout
    {
        private readonly VirtualRowIndex rowIndex;

        public VirtualGridLayout(float rowHeight, int columns, int overscan, VirtualRowIndex rowIndex = null)
        {
            if (rowHeight <= 0 || float.IsNaN(rowHeight) || float.IsInfinity(rowHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(rowHeight));
            }

            if (columns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            if (overscan < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(overscan));
            }

            this.rowIndex = rowIndex;
            RowHeight = rowHeight;
            Columns = columns;
            Overscan = overscan;
        }

        public float RowHeight
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

        /// <summary>几何查询共用行数校验，防止旧的变高索引被用于新的条目数量。</summary>
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

        public float ContentHeight(int count)
        {
            var rows = Rows(count);
            var height = rowIndex == null ? (double)rows * RowHeight : rowIndex.TotalHeight;
            if (height > float.MaxValue)
            {
                throw new InvalidOperationException("Virtual content exceeds supported geometry.");
            }

            return (float)height;
        }

        public float OffsetForIndex(int index) => rowIndex == null
                    ? index / Columns * RowHeight
                    : (float)rowIndex.Offset(Math.Min(index / Columns, rowIndex.Count));

        public float RowHeightForIndex(int index) => rowIndex == null ? RowHeight :
                    rowIndex.Height(index / Columns);

        public int FirstVisible(int count, float offset)
        {
            RequireFinite(offset);
            var rows = Rows(count);
            if (count == 0)
            {
                return -1;
            }

            var row = Math.Min(rows - 1, Math.Max(0,
                rowIndex == null ? Math.Floor(offset / (double)RowHeight) : rowIndex.Bound(offset, true) - 1));
            return (int)row * Columns;
        }

        public void GetRange(int count, float offset, float viewportHeight, out int start, out int end)
        {
            RequireFinite(offset);
            RequireFinite(viewportHeight);

            var rows = Rows(count);
            var top = Math.Max(0, (double)offset);
            var firstRow = rowIndex == null ? Math.Floor(top / RowHeight) : rowIndex.Bound(top, true) - 1;
            var bottom = top + Math.Max(0, viewportHeight);
            var lastRow = rowIndex == null ? Math.Ceiling(bottom / RowHeight) : rowIndex.Bound(bottom, false);
            var first = Math.Min(rows, Math.Max(0, firstRow - Overscan));
            var last = Math.Min(rows, Math.Max(first, lastRow + Overscan));
            start = (int)Math.Min(count, first * Columns);
            end = (int)Math.Min(count, last * Columns);
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
