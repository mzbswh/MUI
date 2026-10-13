using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    [GenerateControlProperty(nameof(Target), "fontSize", "FontSize")]
    [GenerateControlProperty(nameof(Target), "alignment", "Alignment")]
    [GenerateControlProperty(nameof(Target), "supportRichText", "RichText")]
    [GenerateControlProperty(nameof(Target), "fontStyle", "FontStyle")]
    [GenerateControlProperty(nameof(Target), "lineSpacing", "LineSpacing")]
    [GenerateControlProperty(nameof(Target), "horizontalOverflow", "HorizontalOverflow")]
    [GenerateControlProperty(nameof(Target), "verticalOverflow", "VerticalOverflow")]
    [GenerateControlProperty(nameof(Target), "resizeTextForBestFit", "BestFit")]
    [GenerateControlProperty(nameof(Target), "resizeTextMinSize", "MinFontSize")]
    [GenerateControlProperty(nameof(Target), "resizeTextMaxSize", "MaxFontSize")]
    public sealed partial class TextElement
    {
        /// <summary>字号，必须大于零；启用 BestFit 时最终绘制字号由原生布局决定。</summary>
        partial void OnFontSizeChanging(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "字号，必须大于零；启用 BestFit 时最终绘制字号由原生布局决定。");
            }
        }

        /// <summary>行间距倍率，必须为有限值。</summary>
        partial void OnLineSpacingChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "行间距倍率，必须为有限值。");
            }
        }

        /// <summary>
        /// 自动字号下限，接受非负整数，零值保留原生语义。
        /// 与另一端独立更新，不按中间状态裁剪；调用方应保证完整更新后的范围有效。
        /// </summary>
        partial void OnMinFontSizeChanging(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "自动字号边界必须为非负整数。");
            }
        }

        /// <summary>
        /// 自动字号上限，接受非负整数，零值保留原生语义。
        /// 与另一端独立更新，不按中间状态裁剪；调用方应保证完整更新后的范围有效。
        /// </summary>
        partial void OnMaxFontSizeChanging(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "自动字号边界必须为非负整数。");
            }
        }
    }
}
