using System;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class SliderElement
    {
        /// <summary>最小值；原生控件可能同时裁剪当前值，应先配置范围再设置 Value。</summary>
        public float MinValue
        {
            get => Target.minValue;
            set
            {
                RequireFiniteBoundary(value);
                var slider = Target;
                if (slider.minValue.Equals(value))
                {
                    return;
                }

                slider.minValue = value;
                // 范围写入即使发生在隐藏准备阶段，也必须发布实际值变化。
                NotifyChanged(string.Empty);
            }
        }

        /// <summary>最大值；范围最终有效性由调用方保证，不自动交换上下限。</summary>
        public float MaxValue
        {
            get => Target.maxValue;
            set
            {
                RequireFiniteBoundary(value);
                var slider = Target;
                if (slider.maxValue.Equals(value))
                {
                    return;
                }

                slider.maxValue = value;
                NotifyChanged(string.Empty);
            }
        }

        /// <summary>是否取整数；切换时沿用原生舍入并通知当前值变化。</summary>
        public bool WholeNumbers
        {
            get => Target.wholeNumbers;
            set
            {
                var slider = Target;
                if (slider.wholeNumbers == value)
                {
                    return;
                }

                slider.wholeNumbers = value;
                NotifyChanged(string.Empty);
            }
        }

        /// <summary>原生滑动方向；不翻转整个 RectTransform 布局。</summary>
        public Slider.Direction Direction
        {
            get => Target.direction;
            set
            {
                if (!Enum.IsDefined(typeof(Slider.Direction), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var slider = Target;
                if (slider.direction == value)
                {
                    return;
                }

                slider.direction = value;
                NotifyChanged();
            }
        }

        private static void RequireFiniteBoundary(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "滑动条边界必须为有限值。");
            }
        }
    }
}
