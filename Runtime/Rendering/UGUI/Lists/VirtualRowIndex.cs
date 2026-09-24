using System;

namespace MUI.UGUI
{
    /// <summary>树状数组行高索引；单行修正、前缀位置与偏移定位均为 O(log 行数)。</summary>
    internal sealed class VirtualRowIndex
    {
        private readonly float[] heights;
        private readonly double[] tree;
        private readonly int highestBit;

        internal VirtualRowIndex(float[] heights)
        {
            this.heights = heights ?? throw new ArgumentNullException(nameof(heights));
            tree = new double[heights.Length + 1];
            var bit = 1;
            while (bit <= heights.Length / 2)
            {
                bit <<= 1;
            }

            highestBit = bit;
            for (var row = 0; row < heights.Length; ++row)
            {
                ValidateHeight(heights[row]);
                TotalHeight += heights[row];
                ValidateTotal(TotalHeight);
                var index = row + 1;
                tree[index] += heights[row];
                var parent = (long)index + (index & -index);
                if (parent < tree.Length)
                {
                    tree[(int)parent] += tree[index];
                }
            }
        }

        public int Count => heights.Length;

        public double TotalHeight
        {
            get; private set;
        }

        public float Height(int row) => heights[row];

        public double Offset(int row)
        {
            double total = 0;
            for (var index = row; index > 0; index -= index & -index)
            {
                total += tree[index];
            }

            return total;
        }

        public void SetHeight(int row, float height)
        {
            ValidateHeight(height);
            var delta = (double)height - heights[row];
            var total = TotalHeight + delta;
            ValidateTotal(total);
            heights[row] = height;
            TotalHeight = total;
            for (long index = row + 1; index < tree.Length; index += index & -index)
            {
                tree[(int)index] += delta;
            }
        }

        /// <summary>查找首个大于（或大于等于）偏移的行边界，边界范围为 0..Count，无匹配时返回 Count + 1。</summary>
        public int Bound(double offset, bool upper)
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
                if (candidate < offset || (upper && candidate == offset))
                {
                    row = (int)next;
                    total = candidate;
                }
            }

            return row + 1;
        }

        private static void ValidateHeight(float height)
        {
            if (height <= 0 || float.IsNaN(height) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
        }

        internal static void ValidateTotal(double height)
        {
            if (height < 0 || double.IsNaN(height) || double.IsInfinity(height) || height > float.MaxValue)
            {
                throw new InvalidOperationException("Virtual content exceeds supported geometry.");
            }
        }
    }
}
