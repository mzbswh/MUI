using System;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [GenerateControlProperty(nameof(Target), "minValue", "MinValue", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "maxValue", "MaxValue", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "wholeNumbers", "WholeNumbers", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "direction", "Direction")]
    public sealed partial class SliderElement
    {
        /// <summary>最小值；原生控件可能同时裁剪当前值，应先配置范围再设置 Value。</summary>
        partial void OnMinValueChanging(float value)
        {
            RequireFiniteBoundary(value);
        }

        /// <summary>最大值；范围最终有效性由调用方保证，不自动交换上下限。</summary>
        partial void OnMaxValueChanging(float value)
        {
            RequireFiniteBoundary(value);
        }

        /// <summary>原生滑动方向；不翻转整个 RectTransform 布局。</summary>
        partial void OnDirectionChanging(Slider.Direction value)
        {
            if (!Enum.IsDefined(typeof(Slider.Direction), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
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
