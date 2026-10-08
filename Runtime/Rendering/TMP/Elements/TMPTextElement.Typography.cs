using System;
using TMPro;

namespace MUI.TMP
{
    public sealed partial class TMPTextElement
    {
        /// <summary>字号，必须为大于零的有限值；AutoSize 可决定最终绘制字号。</summary>
        public float FontSize
        {
            get => Target.fontSize;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "字号，必须为大于零的有限值；AutoSize 可决定最终绘制字号。");
                }

                var text = Target;
                if (text.fontSize == value)
                {
                    return;
                }

                text.fontSize = value;
                NotifyChanged();
            }
        }

        /// <summary>完整的 TMP 原生对齐选项，保留两端对齐等后端能力。</summary>
        public TextAlignmentOptions Alignment
        {
            get => Target.alignment;
            set
            {
                var text = Target;
                if (text.alignment == value)
                {
                    return;
                }

                text.alignment = value;
                NotifyChanged();
            }
        }

        /// <summary>是否解析富文本标记；显示未经信任的文本时可关闭。</summary>
        public bool RichText
        {
            get => Target.richText;
            set
            {
                var text = Target;
                if (text.richText == value)
                {
                    return;
                }

                text.richText = value;
                NotifyChanged();
            }
        }

        /// <summary>TMP 字体样式，不替换字体资源。</summary>
        public FontStyles FontStyle
        {
            get => Target.fontStyle;
            set
            {
                var text = Target;
                if (text.fontStyle == value)
                {
                    return;
                }

                text.fontStyle = value;
                NotifyChanged();
            }
        }

        /// <summary>字符间距，允许有限负值。</summary>
        public float CharacterSpacing
        {
            get => Target.characterSpacing;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "字符间距，允许有限负值。");
                }

                var text = Target;
                if (text.characterSpacing == value)
                {
                    return;
                }

                text.characterSpacing = value;
                NotifyChanged();
            }
        }

        /// <summary>单词间距，允许有限负值。</summary>
        public float WordSpacing
        {
            get => Target.wordSpacing;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "单词间距，允许有限负值。");
                }

                var text = Target;
                if (text.wordSpacing == value)
                {
                    return;
                }

                text.wordSpacing = value;
                NotifyChanged();
            }
        }

        /// <summary>行间距，允许有限负值。</summary>
        public float LineSpacing
        {
            get => Target.lineSpacing;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "行间距，允许有限负值。");
                }

                var text = Target;
                if (text.lineSpacing == value)
                {
                    return;
                }

                text.lineSpacing = value;
                NotifyChanged();
            }
        }

        /// <summary>段落间距，允许有限负值。</summary>
        public float ParagraphSpacing
        {
            get => Target.paragraphSpacing;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "段落间距，允许有限负值。");
                }

                var text = Target;
                if (text.paragraphSpacing == value)
                {
                    return;
                }

                text.paragraphSpacing = value;
                NotifyChanged();
            }
        }

        /// <summary>是否使用原生自动字号；上下限通过 MinFontSize/MaxFontSize 配置。</summary>
        public bool AutoSize
        {
            get => Target.enableAutoSizing;
            set
            {
                var text = Target;
                if (text.enableAutoSizing == value)
                {
                    return;
                }

                text.enableAutoSizing = value;
                NotifyChanged();
            }
        }

        /// <summary>是否自动换行，适配当前 TMP 版本的原生接口。</summary>
        public bool WordWrapping
        {
            get => Target.enableWordWrapping;
            set
            {
                var text = Target;
                if (text.enableWordWrapping == value)
                {
                    return;
                }

                text.enableWordWrapping = value;
                NotifyChanged();
            }
        }

        /// <summary>文本溢出策略。</summary>
        public TextOverflowModes OverflowMode
        {
            get => Target.overflowMode;
            set
            {
                var text = Target;
                if (text.overflowMode == value)
                {
                    return;
                }

                text.overflowMode = value;
                NotifyChanged();
            }
        }

        /// <summary>最多显示的字符数量，可用于逐字显示，必须非负。</summary>
        public int MaxVisibleCharacters
        {
            get => Target.maxVisibleCharacters;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "最多显示的字符数量，可用于逐字显示，必须非负。");
                }

                var text = Target;
                if (text.maxVisibleCharacters == value)
                {
                    return;
                }

                text.maxVisibleCharacters = value;
                NotifyChanged();
            }
        }

        /// <summary>启用 TMP 从右到左显示；不代表完整语言塑形与本地化处理。</summary>
        public bool RightToLeft
        {
            get => Target.isRightToLeftText;
            set
            {
                var text = Target;
                if (text.isRightToLeftText == value)
                {
                    return;
                }

                text.isRightToLeftText = value;
                NotifyChanged();
            }
        }

        /// <summary>
        /// 自动字号下限，接受非负有限值，零值保留原生语义。
        /// 与另一端独立更新，不按中间状态裁剪；调用方应保证完整更新后的范围有效。
        /// </summary>
        public float MinFontSize
        {
            get => Target.fontSizeMin;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "自动字号边界必须为非负有限值。");
                }

                var text = Target;
                if (text.fontSizeMin == value)
                {
                    return;
                }

                text.fontSizeMin = value;
                NotifyChanged();
            }
        }

        /// <summary>
        /// 自动字号上限，接受非负有限值，零值保留原生语义。
        /// 与另一端独立更新，不按中间状态裁剪；调用方应保证完整更新后的范围有效。
        /// </summary>
        public float MaxFontSize
        {
            get => Target.fontSizeMax;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "自动字号边界必须为非负有限值。");
                }

                var text = Target;
                if (text.fontSizeMax == value)
                {
                    return;
                }

                text.fontSizeMax = value;
                NotifyChanged();
            }
        }
    }
}
