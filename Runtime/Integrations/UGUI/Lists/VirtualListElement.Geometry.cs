using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField]
        private Vector2 spacing;
        [SerializeField]
        private RectOffset padding = new RectOffset();

        /// <summary>初始化前确定滚动轴；横向仅支持单列，Grid 沿纵向滚动。</summary>
        public RectTransform.Axis ScrollAxis => scrollAxis;

        private bool IsHorizontal => scrollAxis == RectTransform.Axis.Horizontal;

        private float ScrollOffset => IsHorizontal ? -scrollRect.content.anchoredPosition.x : scrollRect.content.anchoredPosition.y;

        private float ScrollVelocity => IsHorizontal ? scrollRect.velocity.x : scrollRect.velocity.y;

        private float ViewportExtent => IsHorizontal ? scrollRect.viewport.rect.width : scrollRect.viewport.rect.height;

        private float MainSpacing => IsHorizontal ? spacing.x : spacing.y;

        private float LeadingPadding => IsHorizontal ? padding.left : padding.top;

        private float TrailingPadding => IsHorizontal ? padding.right : padding.bottom;

        private float AvailableGridWidth => Math.Max(0, scrollRect.content.rect.width - (float)padding.left - padding.right);

        private float ItemCrossExtent => IsHorizontal
            ? Math.Max(0, scrollRect.content.rect.height - (float)padding.top - padding.bottom)
            : (float)Math.Max(0, (AvailableGridWidth - (columns - 1d) * spacing.x) / columns);

        private VirtualGridLayout CreateLayout(int count, int extraRows, VirtualRowIndex offsets) =>
            new VirtualGridLayout(estimatedItemExtent, count, extraRows, offsets,
                MainSpacing, LeadingPadding, TrailingPadding);

        /// <summary>初始化前配置物理 X/Y 方向间距及四边内边距；复制内边距以隔离调用方后续修改。</summary>
        public void ConfigureSpacing(Vector2 itemSpacing, RectOffset contentPadding)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Configure spacing before initialization.");
            }
            ValidateSpacing(itemSpacing, contentPadding);
            spacing = itemSpacing;
            padding = new RectOffset(contentPadding.left, contentPadding.right, contentPadding.top, contentPadding.bottom);
        }

        private static void ValidateSpacing(Vector2 itemSpacing, RectOffset contentPadding)
        {
            if (itemSpacing.x < 0 || itemSpacing.y < 0 || float.IsNaN(itemSpacing.x) ||
                float.IsNaN(itemSpacing.y) || float.IsInfinity(itemSpacing.x) || float.IsInfinity(itemSpacing.y) ||
                contentPadding == null || contentPadding.left < 0 || contentPadding.right < 0 ||
                contentPadding.top < 0 || contentPadding.bottom < 0)
            {
                throw new ArgumentException("Virtual list spacing and padding must be finite and nonnegative.");
            }
        }

        private static void ValidateScrollAxis(RectTransform.Axis axis, int columnCount)
        {
            if (axis != RectTransform.Axis.Horizontal && axis != RectTransform.Axis.Vertical)
            {
                throw new ArgumentOutOfRangeException(nameof(axis));
            }

            if (axis == RectTransform.Axis.Horizontal && columnCount != 1)
            {
                throw new ArgumentException("Horizontal virtual lists require one column.", nameof(columnCount));
            }
        }

        private void ConfigureAxisAnchors(RectTransform rect)
        {
            rect.anchorMin = IsHorizontal ? Vector2.zero : new Vector2(0, 1);
            rect.anchorMax = IsHorizontal ? new Vector2(0, 1) : Vector2.one;
            rect.pivot = IsHorizontal ? new Vector2(0, 0.5f) : new Vector2(0.5f, 1);
        }

        private void PositionCell(RectTransform rect, int index, float extent)
        {
            using var sample = cellLayoutMarker.Auto();
            if (IsHorizontal)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(0, 1);
                rect.offsetMin = new Vector2(rect.offsetMin.x, padding.bottom);
                rect.offsetMax = new Vector2(rect.offsetMax.x, -padding.top);
                rect.anchoredPosition = new Vector2(Layout.OffsetForIndex(index), (padding.bottom - (float)padding.top) * 0.5f);
            }
            else
            {
                var column = index % columns;
                rect.anchorMin = new Vector2(column / (float)columns, 1);
                rect.anchorMax = new Vector2((column + 1) / (float)columns, 1);
                // 保留按列锚点，偏移扣除内边距和列间距，使宽度变化时仍可由锚点拉伸。
                var left = padding.left + column * (ItemCrossExtent + spacing.x) -
                    scrollRect.content.rect.width * column / columns;
                var right = padding.left + column * (ItemCrossExtent + spacing.x) + ItemCrossExtent -
                    scrollRect.content.rect.width * (column + 1f) / columns;
                rect.offsetMin = new Vector2(left, rect.offsetMin.y);
                rect.offsetMax = new Vector2(right, rect.offsetMax.y);
                rect.anchoredPosition = new Vector2((left + right) * 0.5f, -Layout.OffsetForIndex(index));
            }

            rect.SetSizeWithCurrentAnchors(IsHorizontal ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal,
                ItemCrossExtent);
            rect.SetSizeWithCurrentAnchors(scrollAxis, extent);
        }
    }
}
