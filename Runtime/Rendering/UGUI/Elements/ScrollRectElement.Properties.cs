using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class ScrollRectElement
    {
        /// <summary>内容锚点坐标；不同于归一化位置，不裁剪，可用于程序定位。</summary>
        public Vector2 ContentPosition
        {
            get => RequireContent().anchoredPosition;
            set
            {
                RequireFiniteVector(value);
                var content = RequireContent();
                if (content.anchoredPosition.Equals(value))
                {
                    return;
                }

                Target.StopMovement();
                content.anchoredPosition = value;
                PublishPosition();
            }
        }

        /// <summary>原生滚动速度；通知来自原生滚动事件和 Element 写入，不独立逐帧轮询。</summary>
        public Vector2 Velocity
        {
            get => Target.velocity;
            set
            {
                RequireFiniteVector(value);
                RequireContent();
                var control = Target;
                if (control.velocity.Equals(value))
                {
                    return;
                }

                control.velocity = value;
                PublishPosition();
            }
        }

        /// <summary>是否允许原生水平滚动。</summary>
        public bool Horizontal
        {
            get => Target.horizontal;
            set
            {
                var control = Target;
                if (control.horizontal == value)
                {
                    return;
                }

                control.horizontal = value;
                NotifyChanged();
            }
        }

        /// <summary>是否允许原生垂直滚动。</summary>
        public bool Vertical
        {
            get => Target.vertical;
            set
            {
                var control = Target;
                if (control.vertical == value)
                {
                    return;
                }

                control.vertical = value;
                NotifyChanged();
            }
        }

        string IBindingPropertyPolicy.GetBindingWriteTarget(string propertyName, BindingMode mode) =>
                    propertyName == nameof(ContentPosition) ? nameof(NormalizedPosition) : propertyName;

        private RectTransform RequireContent()
        {
            var content = Target.content;
            if (content == null)
            {
                throw new InvalidOperationException("滚动容器缺少 Content。");
            }

            return content;
        }

        private static void RequireFiniteVector(Vector2 value)
        {
            if (!Finite(value.x) || !Finite(value.y))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "滚动坐标或速度必须为有限值。");
            }
        }
    }
}
