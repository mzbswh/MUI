using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>普通 ScrollRect 位置绑定，不能用于虚拟列表的 ScrollRect。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ScrollRect))]
    public sealed partial class ScrollRectElement : Element, IBindingPropertyPolicy
    {
        private ScrollRect target;
        private Vector2 lastNotified;
        private Vector2 lastContentPosition;
        private Vector2 lastVelocity;

        private ScrollRect Target
        {
            get
            {
                RequireAlive();
                if (target == null)
                {
                    target = RequireComponent<ScrollRect>();
                }

                return target;
            }
        }

        /// <summary>原生归一化坐标：左和下为零，右和上为一。</summary>
        public Vector2 NormalizedPosition
        {
            get => Target.normalizedPosition;
            set
            {
                if (!Finite(value.x) || !Finite(value.y))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scroll position must be finite.");
                }

                var control = Target;
                if (control.content == null)
                {
                    throw new InvalidOperationException("ScrollRect content is missing.");
                }

                // 双向绑定会回传原生值，包含弹性越界位置。
                // 相同值的回传不能停止惯性或钳制进行中的拖拽。
                if (control.normalizedPosition.Equals(value))
                {
                    return;
                }

                control.StopMovement();
                control.normalizedPosition = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
                PublishPosition();
            }
        }

        public void StopMovement()
        {
            Target.StopMovement();
            PublishPosition();
        }

        protected override void OnInitialize()
        {
            target = RequireComponent<ScrollRect>();
            if (target.content == null)
            {
                throw new InvalidOperationException("ScrollRectElement requires a content RectTransform.");
            }

            lastNotified = target.normalizedPosition;
            lastContentPosition = target.content.anchoredPosition;
            lastVelocity = target.velocity;
            UnityAction<Vector2> changed = value =>
            {
                if (!IsAlive || !isActiveAndEnabled || target == null || !target.isActiveAndEnabled)
                {
                    return;
                }

                var owner = GetComponentInParent<View>(true);
                if (owner != null && !owner.IsInputEnabled)
                {
                    return;
                }

                PublishPosition();
            };
            OnDispose(() =>
            {
                if (target != null)
                {
                    target.onValueChanged.RemoveListener(changed);
                }
            });
            target.onValueChanged.AddListener(changed);
        }

        private void PublishPosition()
        {
            var control = Target;
            if (control.content == null)
            {
                throw new InvalidOperationException("ScrollRect content is missing.");
            }

            var current = control.normalizedPosition;
            var position = control.content.anchoredPosition;
            var velocity = control.velocity;
            if (lastNotified.Equals(current) && lastContentPosition.Equals(position) && lastVelocity.Equals(velocity))
            {
                return;
            }

            lastNotified = current;
            lastContentPosition = position;
            lastVelocity = velocity;
            // 两种位置坐标和速度由同一原生滚动状态决定，一次发布当前快照。
            NotifyChanged(string.Empty);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
