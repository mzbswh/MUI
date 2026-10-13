using System;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [GenerateControlProperty(nameof(Target), "type", "ImageType")]
    [GenerateControlProperty(nameof(Target), "preserveAspect", "PreserveAspect")]
    [GenerateControlProperty(nameof(Target), "fillCenter", "FillCenter")]
    [GenerateControlProperty(nameof(Target), "fillMethod", "FillMethod", NotifyAll = true)]
    [GenerateControlProperty(nameof(Target), "fillOrigin", "FillOrigin")]
    [GenerateControlProperty(nameof(Target), "fillClockwise", "FillClockwise")]
    public sealed partial class ImageElement
    {
        /// <summary>图片绘制类型。</summary>
        partial void OnImageTypeChanging(Image.Type value)
        {
            if (!Enum.IsDefined(typeof(Image.Type), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>填充方式；原生控件会同时将 FillOrigin 重置为零。</summary>
        partial void OnFillMethodChanging(Image.FillMethod value)
        {
            if (!Enum.IsDefined(typeof(Image.FillMethod), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>填充起点，取值 0–3；具体有效起点由 FillMethod 决定。</summary>
        partial void OnFillOriginChanging(int value)
        {
            if (value < 0 || value > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

    }
}
