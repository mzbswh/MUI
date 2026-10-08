using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField]
        private bool followEnd;
        [SerializeField, Min(0)]
        private float endFollowTolerance = 2;

        /// <summary>仅更新前已靠近末尾时继续跟随；默认关闭，不主动改变当前位置。</summary>
        public bool FollowEnd
        {
            get => followEnd;
            set
            {
                RequireListAlive();
                followEnd = value;
            }
        }

        /// <summary>末尾跟随判定容差，单位为 Content 局部 Canvas 单位。</summary>
        public float EndFollowTolerance
        {
            get => endFollowTolerance;
            set
            {
                RequireListAlive();
                if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                endFollowTolerance = value;
            }
        }

        private float EndOffset => Math.Max(0, Layout.ContentExtent(snapshot.Count) - ViewportExtent);

        private bool ShouldFollowEnd() => initialized && followEnd && activeReveal == null &&
            (scrollInput == null || !scrollInput.IsDragging) && EndOffset - ScrollOffset <= endFollowTolerance;

        /// <summary>阅读补偿保留惯性，并重置原生拖动基准，避免下一次拖动回到补偿前的位置。</summary>
        private void SetReadingOffset(float offset, bool preserveVelocity = true)
        {
            var revision = sourceRevision;
            var request = revealRevision;
            var velocity = preserveVelocity ? scrollRect.velocity : Vector2.zero;
            SetOffset(offset);
            if (!IsAlive || revision != sourceRevision || request != revealRevision || scrollRect == null)
            {
                return;
            }

            // PostLayout 同步上一帧位置记录，否则原生拖动会把补偿位移当作新速度。
            scrollRect.Rebuild(UnityEngine.UI.CanvasUpdate.PostLayout);
            if (!IsAlive || revision != sourceRevision || request != revealRevision || scrollRect == null)
            {
                return;
            }

            scrollRect.velocity = velocity;
            if (scrollInput != null)
            {
                scrollInput.RebaseDrag(scrollRect);
            }
        }
    }
}
