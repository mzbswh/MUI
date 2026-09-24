using System;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class ScrollbarElement
    {
        /// <summary>手柄占轨道比例；有限输入沿原生语义裁剪到 0–1。</summary>
        public float Size
        {
            get => Target.size;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var control = Target;
                var previous = control.size;
                if (previous.Equals(value))
                {
                    return;
                }

                control.size = value;
                if (IsAlive && control != null && !previous.Equals(control.size))
                {
                    NotifyChanged();
                }
            }
        }

        /// <summary>离散步数；零或一沿原生语义保持连续值，大于一启用分档。</summary>
        public int NumberOfSteps
        {
            get => Target.numberOfSteps;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var control = Target;
                var previous = control.numberOfSteps;
                if (previous.Equals(value))
                {
                    return;
                }

                control.numberOfSteps = value;
                if (IsAlive && control != null && !previous.Equals(control.numberOfSteps))
                {
                    NotifyChanged(string.Empty);
                }
            }
        }

        /// <summary>滚动条方向，不翻转整个 RectTransform。</summary>
        public Scrollbar.Direction Direction
        {
            get => Target.direction;
            set
            {
                if (!Enum.IsDefined(typeof(Scrollbar.Direction), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var control = Target;
                var previous = control.direction;
                if (previous.Equals(value))
                {
                    return;
                }

                control.direction = value;
                if (IsAlive && control != null && !previous.Equals(control.direction))
                {
                    NotifyChanged();
                }
            }
        }
    }
}
