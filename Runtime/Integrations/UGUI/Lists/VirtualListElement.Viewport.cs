using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private VirtualListViewport publishedViewport;
        private bool hasPublishedViewport;
        private float publishedViewportOffset;
        private int viewportMovementFrame = -1;
        private VirtualListViewportObserver viewportObserver;
        private bool pendingViewportResize;
        private float viewportCrossExtent;
        private int resizeAnchor;
        private float resizeAnchorOffset;
        private bool resizeFollowEnd;
        private long resizeSourceRevision;
        private long resizeRevealRevision;

        /// <summary>
        /// 查询当前可见范围与边界距离。变化通过 Element 属性通知发布，按帧合并；
        /// 加载更多等业务可观察此属性，再更新 Items，不在布局提交栈内启动分页。
        /// </summary>
        public VirtualListViewport Viewport
        {
            get
            {
                RequireListAlive();
                return ReadViewport();
            }
        }

        private void InitializeViewportObserver()
        {
            if (scrollRect.viewport.GetComponent<VirtualListViewportObserver>() != null)
            {
                throw new InvalidOperationException("A viewport cannot be owned by multiple virtual lists.");
            }

            viewportExtent = ViewportExtent;
            viewportCrossExtent = ItemCrossExtent;
            viewportObserver = scrollRect.viewport.gameObject.AddComponent<VirtualListViewportObserver>();
            viewportObserver.Attach(this);
            OnDispose(() =>
            {
                if (viewportObserver != null)
                {
                    viewportObserver.Attach(null);
                    Destroy(viewportObserver);
                    viewportObserver = null;
                }
            });
        }

        internal void CaptureViewportResize()
        {
            if (!initialized || !IsAlive || scrollRect == null || scrollRect.viewport == null)
            {
                return;
            }

            var nextExtent = ViewportExtent;
            var nextCrossExtent = ItemCrossExtent;
            if (viewportExtent == nextExtent && viewportCrossExtent == nextCrossExtent)
            {
                return;
            }

            // 同一布局过程可连续通知多次；保留第一次变更前的阅读位置。
            if (!pendingViewportResize && scope != null && scope.IsActive &&
                !resettingActivation && Error == null && activeReveal == null && !IsVisualRetentionActive)
            {
                resizeAnchor = FirstVisibleIndex;
                resizeAnchorOffset = resizeAnchor < 0 ? 0 : ScrollOffset - Layout.OffsetForIndex(resizeAnchor);
                var oldEnd = Math.Max(0, Layout.ContentExtent(snapshot.Count) - viewportExtent);
                resizeFollowEnd = followEnd && (scrollInput == null || !scrollInput.IsDragging) &&
                    oldEnd - ScrollOffset <= endFollowTolerance;
                resizeSourceRevision = sourceRevision;
                resizeRevealRevision = revealRevision;
                pendingViewportResize = true;
            }

            viewportExtent = nextExtent;
            viewportCrossExtent = nextCrossExtent;
            first = last = -1;
        }

        private void ApplyViewportResize()
        {
            if (!pendingViewportResize)
            {
                return;
            }

            pendingViewportResize = false;
            // 后续换源、增量提交或显式定位已经拥有当前位置，不能被旧布局通知覆盖。
            if (resizeSourceRevision != sourceRevision || resizeRevealRevision != revealRevision ||
                scope == null || !scope.IsActive || activeReveal != null || IsVisualRetentionActive)
            {
                return;
            }

            var offset = resizeAnchor < 0 ? 0 : Layout.OffsetForIndex(resizeAnchor) +
                Math.Min(resizeAnchorOffset, Layout.RowExtentForIndex(resizeAnchor));
            SetReadingOffset(resizeFollowEnd ? EndOffset : offset);
        }

        private VirtualListViewport ReadViewport()
        {
            if (!initialized || scope == null || !scope.IsActive || Error != null ||
                scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
            {
                return default;
            }

            var offset = ScrollOffset;
            var extent = ViewportExtent;
            if (float.IsNaN(offset) || float.IsInfinity(offset) ||
                float.IsNaN(extent) || float.IsInfinity(extent))
            {
                throw new InvalidOperationException("Virtual viewport geometry must be finite.");
            }

            var layout = CreateLayout(columns, 0, rowIndex);
            var contentExtent = layout.ContentExtent(snapshot.Count);
            // 弹性越界时只统计与实际视口相交的内容，不能把负偏移当成起点。
            var visibleStart = Math.Max(0, offset);
            var visibleEnd = Math.Min(contentExtent, offset + Math.Max(0, extent));
            var start = 0;
            var end = 0;
            if (visibleEnd > visibleStart)
            {
                layout.GetRange(snapshot.Count, visibleStart, visibleEnd - visibleStart, out start, out end);
            }

            var maximumOffset = Math.Max(0, contentExtent - Math.Max(0, extent));
            var boundedOffset = Mathf.Clamp(offset, 0, maximumOffset);
            var moving = isActiveAndEnabled && scrollRect.isActiveAndEnabled &&
                (ScrollVelocity != 0 || viewportMovementFrame == Time.frameCount ||
                 (hasPublishedViewport && !publishedViewportOffset.Equals(offset)));
            return new VirtualListViewport(start, end, boundedOffset, maximumOffset - boundedOffset, moving);
        }

        private void PublishViewport()
        {
            var current = ReadViewport();
            var offset = scrollRect == null || scrollRect.content == null ? 0 : ScrollOffset;
            if (hasPublishedViewport && !publishedViewportOffset.Equals(offset))
            {
                viewportMovementFrame = Time.frameCount;
            }

            publishedViewportOffset = offset;
            var changed = !hasPublishedViewport || !publishedViewport.Equals(current);
            hasPublishedViewport = true;
            publishedViewport = current;
            if (!changed)
            {
                return;
            }

            // 先提交快照再调用业务观察者；观察者修改来源会在下一帧合并发布。
            try
            {
                NotifyChanged(nameof(Viewport));
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
