using System;

namespace MUI.UGUI
{
    /// <summary>列表当前视口的几何快照；不包含预留行，也不要求条目已完成物化。</summary>
    public readonly struct VirtualListViewport : IEquatable<VirtualListViewport>
    {
        internal VirtualListViewport(int startIndex, int endIndex, float distanceFromStart,
            float distanceFromEnd, bool isScrolling)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            DistanceFromStart = distanceFromStart;
            DistanceFromEnd = distanceFromEnd;
            IsScrolling = isScrolling;
        }

        /// <summary>可见范围起点，包含该索引；空范围以 StartIndex == EndIndex 表示。</summary>
        public int StartIndex
        {
            get;
        }

        /// <summary>可见范围终点，不包含该索引。</summary>
        public int EndIndex
        {
            get;
        }

        public int VisibleCount => EndIndex - StartIndex;

        /// <summary>距内容起点的非负距离，单位为 Content 局部 Canvas 单位。</summary>
        public float DistanceFromStart
        {
            get;
        }

        /// <summary>距内容末尾的非负距离；内容小于视口时为零。</summary>
        public float DistanceFromEnd
        {
            get;
        }

        /// <summary>本帧位置发生变化或原生滚动仍有速度；静止按住指针不算滚动。</summary>
        public bool IsScrolling
        {
            get;
        }

        public bool Equals(VirtualListViewport other) => StartIndex == other.StartIndex &&
            EndIndex == other.EndIndex && DistanceFromStart.Equals(other.DistanceFromStart) &&
            DistanceFromEnd.Equals(other.DistanceFromEnd) && IsScrolling == other.IsScrolling;

        public override bool Equals(object obj) => obj is VirtualListViewport other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StartIndex * 397 ^ EndIndex;
                hash = hash * 397 ^ DistanceFromStart.GetHashCode();
                hash = hash * 397 ^ DistanceFromEnd.GetHashCode();
                return hash * 397 ^ IsScrolling.GetHashCode();
            }
        }
    }
}
