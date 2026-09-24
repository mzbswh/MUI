using System;
using UnityEngine;

namespace MUI.UGUI
{
    public enum OverlayPlacement
    {
        Below,
        Above,
        Left,
        Right
    }

    /// <summary>在单一 RectTransform 坐标系中计算位置，独立于场景所有权。</summary>
    public static class AnchoredOverlayPlacement
    {
        public static Rect Calculate(Rect anchor, Vector2 size, Rect bounds, OverlayPlacement preferred, float gap, out OverlayPlacement actual)
        {
            if (!Enum.IsDefined(typeof(OverlayPlacement), preferred))
            {
                throw new ArgumentOutOfRangeException(nameof(preferred));
            }

            Validate(anchor, nameof(anchor));
            Validate(bounds, nameof(bounds));
            if (!Finite(size.x) || !Finite(size.y) || size.x < 0 || size.y < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if (!Finite(gap) || gap < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gap));
            }

            actual = preferred;
            var candidate = Place(anchor, size, preferred, gap);
            var opposite = Opposite(preferred);
            var alternative = Place(anchor, size, opposite, gap);
            if (Overflow(alternative, bounds) < Overflow(candidate, bounds))
            {
                candidate = alternative;
                actual = opposite;
            }

            // 超大内容居中放置，缩放与滚动仍由内容布局负责。
            candidate.x = size.x > bounds.width ? bounds.center.x - size.x * 0.5f : Mathf.Clamp(candidate.x, bounds.xMin, bounds.xMax - size.x);
            candidate.y = size.y > bounds.height ? bounds.center.y - size.y * 0.5f : Mathf.Clamp(candidate.y, bounds.yMin, bounds.yMax - size.y);
            Validate(candidate, nameof(candidate));
            return candidate;
        }

        private static Rect Place(Rect anchor, Vector2 size, OverlayPlacement side, float gap)
        {
            var x = anchor.center.x - size.x * 0.5f;
            var y = anchor.center.y - size.y * 0.5f;
            switch (side)
            {
                case OverlayPlacement.Below:
                    y = anchor.yMin - gap - size.y;
                    break;
                case OverlayPlacement.Above:
                    y = anchor.yMax + gap;
                    break;
                case OverlayPlacement.Left:
                    x = anchor.xMin - gap - size.x;
                    break;
                case OverlayPlacement.Right:
                    x = anchor.xMax + gap;
                    break;
            }

            return new Rect(x, y, size.x, size.y);
        }

        private static OverlayPlacement Opposite(OverlayPlacement side)
        {
            switch (side)
            {
                case OverlayPlacement.Below:
                    return OverlayPlacement.Above;
                case OverlayPlacement.Above:
                    return OverlayPlacement.Below;
                case OverlayPlacement.Left:
                    return OverlayPlacement.Right;
                default:
                    return OverlayPlacement.Left;
            }
        }

        private static double Overflow(Rect value, Rect bounds) => Math.Max(0d, (double)bounds.xMin - value.xMin) + Math.Max(0d, (double)value.xMax - bounds.xMax) + Math.Max(0d, (double)bounds.yMin - value.yMin) + Math.Max(0d, (double)value.yMax - bounds.yMax);

        private static void Validate(Rect value, string name)
        {
            if (!Finite(value.xMin) || !Finite(value.yMin) || !Finite(value.xMax) || !Finite(value.yMax) || value.width < 0 || value.height < 0)
            {
                throw new ArgumentOutOfRangeException(name, "Rectangle must be finite and nonnegative.");
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
