using System;
using TMPro;

namespace MUI.TMP
{
    [GenerateControlProperty(nameof(Target), "fontSize", "FontSize")]
    [GenerateControlProperty(nameof(Target), "alignment", "Alignment")]
    [GenerateControlProperty(nameof(Target), "richText", "RichText")]
    [GenerateControlProperty(nameof(Target), "fontStyle", "FontStyle")]
    [GenerateControlProperty(nameof(Target), "characterSpacing", "CharacterSpacing")]
    [GenerateControlProperty(nameof(Target), "wordSpacing", "WordSpacing")]
    [GenerateControlProperty(nameof(Target), "lineSpacing", "LineSpacing")]
    [GenerateControlProperty(nameof(Target), "paragraphSpacing", "ParagraphSpacing")]
    [GenerateControlProperty(nameof(Target), "enableAutoSizing", "AutoSize")]
    [GenerateControlProperty(nameof(Target), "enableWordWrapping", "WordWrapping")]
    [GenerateControlProperty(nameof(Target), "overflowMode", "OverflowMode")]
    [GenerateControlProperty(nameof(Target), "maxVisibleCharacters", "MaxVisibleCharacters")]
    [GenerateControlProperty(nameof(Target), "isRightToLeftText", "RightToLeft")]
    [GenerateControlProperty(nameof(Target), "fontSizeMin", "MinFontSize")]
    [GenerateControlProperty(nameof(Target), "fontSizeMax", "MaxFontSize")]
    public sealed partial class TMPTextElement
    {
        /// <summary>字号，必须为大于零的有限值；AutoSize 可决定最终绘制字号。</summary>
        partial void OnFontSizeChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "字号，必须为大于零的有限值；AutoSize 可决定最终绘制字号。");
            }
        }

        /// <summary>字符间距，允许有限负值。</summary>
        partial void OnCharacterSpacingChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "字符间距，允许有限负值。");
            }
        }

        /// <summary>单词间距，允许有限负值。</summary>
        partial void OnWordSpacingChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "单词间距，允许有限负值。");
            }
        }

        /// <summary>行间距，允许有限负值。</summary>
        partial void OnLineSpacingChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "行间距，允许有限负值。");
            }
        }

        /// <summary>段落间距，允许有限负值。</summary>
        partial void OnParagraphSpacingChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "段落间距，允许有限负值。");
            }
        }

        /// <summary>最多显示的字符数量，可用于逐字显示，必须非负。</summary>
        partial void OnMaxVisibleCharactersChanging(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "最多显示的字符数量，可用于逐字显示，必须非负。");
            }
        }

        /// <summary>
        /// 自动字号下限，接受非负有限值，零值保留原生语义。
        /// 与另一端独立更新，不按中间状态裁剪；调用方应保证完整更新后的范围有效。
        /// </summary>
        partial void OnMinFontSizeChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "自动字号边界必须为非负有限值。");
            }
        }

        /// <summary>
        /// 自动字号上限，接受非负有限值，零值保留原生语义。
        /// 与另一端独立更新，不按中间状态裁剪；调用方应保证完整更新后的范围有效。
        /// </summary>
        partial void OnMaxFontSizeChanging(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "自动字号边界必须为非负有限值。");
            }
        }
    }
}
