using System;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [GenerateControlProperty(nameof(Target), "size", "Size")]
    [GenerateControlProperty(nameof(Target), "numberOfSteps", "NumberOfSteps", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "direction", "Direction")]
    public sealed partial class ScrollbarElement
    {
        /// <summary>手柄占轨道比例；有限输入沿原生语义裁剪到 0–1。</summary>
        partial void OnSizeChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>离散步数；零或一沿原生语义保持连续值，大于一启用分档。</summary>
        partial void OnNumberOfStepsChanging(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>滚动条方向，不翻转整个 RectTransform。</summary>
        partial void OnDirectionChanging(Scrollbar.Direction value)
        {
            if (!Enum.IsDefined(typeof(Scrollbar.Direction), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
