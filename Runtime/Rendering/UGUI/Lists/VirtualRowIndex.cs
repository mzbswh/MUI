using System;

namespace MUI.UGUI
{
    /// <summary>沿滚动轴的树状数组尺寸索引；单行修正、前缀位置与偏移定位均为 O(log 行数)。</summary>
    internal sealed class VirtualRowIndex
    {
        private readonly float[] extents;
        private readonly double[] tree;
        private readonly int highestBit;

        internal VirtualRowIndex(float[] extents)
        {
            this.extents = extents ?? throw new ArgumentNullException(nameof(extents));
            tree = new double[extents.Length + 1];
            var bit = 1;
            while (bit <= extents.Length / 2)
            {
                bit <<= 1;
            }

            highestBit = bit;
            for (var row = 0; row < extents.Length; ++row)
            {
                ValidateExtent(extents[row]);
                TotalExtent += extents[row];
                ValidateTotal(TotalExtent);
                var index = row + 1;
                tree[index] += extents[row];
                var parent = (long)index + (index & -index);
                if (parent < tree.Length)
                {
                    tree[(int)parent] += tree[index];
                }
            }
        }

        public int Count => extents.Length;

        public double TotalExtent
        {
            get; private set;
        }

        public float Extent(int row) => extents[row];

        public double Offset(int row)
        {
            double total = 0;
            for (var index = row; index > 0; index -= index & -index)
            {
                total += tree[index];
            }

            return total;
        }

        public void SetExtent(int row, float extent)
        {
            ValidateExtent(extent);
            var delta = (double)extent - extents[row];
            var total = TotalExtent + delta;
            ValidateTotal(total);
            extents[row] = extent;
            TotalExtent = total;
            for (long index = row + 1; index < tree.Length; index += index & -index)
            {
                tree[(int)index] += delta;
            }
        }

        /// <summary>查找首个大于（或大于等于）偏移的行边界，边界范围为 0..Count，无匹配时返回 Count + 1。</summary>
        public int Bound(double offset, bool upper, double spacing = 0)
        {
            if (offset < 0 || (!upper && offset == 0))
            {
                return 0;
            }

            var row = 0;
            double total = 0;
            // 沿树状数组二进制步进，避免对每次二分再求一次前缀和。
            for (var bit = highestBit; bit > 0; bit >>= 1)
            {
                var next = (long)row + bit;
                if (next >= tree.Length)
                {
                    continue;
                }

                var candidate = total + tree[(int)next];
                if (candidate + next * spacing < offset || (upper && candidate + next * spacing == offset))
                {
                    row = (int)next;
                    total = candidate;
                }
            }

            return row + 1;
        }

        private static void ValidateExtent(float extent)
        {
            if (extent <= 0 || float.IsNaN(extent) || float.IsInfinity(extent))
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }
        }

        internal static void ValidateTotal(double extent)
        {
            if (extent < 0 || double.IsNaN(extent) || double.IsInfinity(extent) || extent > float.MaxValue)
            {
                throw new InvalidOperationException("Virtual content exceeds supported geometry.");
            }
        }
    }
}
