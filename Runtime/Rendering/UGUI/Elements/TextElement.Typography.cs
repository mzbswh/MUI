using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class TextElement
    {
        /// <summary>字号，必须大于零；启用 BestFit 时最终绘制字号由原生布局决定。</summary>
        public int FontSize
        {
            get => Target.fontSize;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "字号，必须大于零；启用 BestFit 时最终绘制字号由原生布局决定。");
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

        /// <summary>原生九宫格文字对齐。</summary>
        public TextAnchor Alignment
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
            get => Target.supportRichText;
            set
            {
                var text = Target;
                if (text.supportRichText == value)
                {
                    return;
                }

                text.supportRichText = value;
                NotifyChanged();
            }
        }

        /// <summary>字体样式，不替换字体资源。</summary>
        public FontStyle FontStyle
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

        /// <summary>行间距倍率，必须为有限值。</summary>
        public float LineSpacing
        {
            get => Target.lineSpacing;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "行间距倍率，必须为有限值。");
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

        /// <summary>水平换行或溢出策略。</summary>
        public HorizontalWrapMode HorizontalOverflow
        {
            get => Target.horizontalOverflow;
            set
            {
                var text = Target;
                if (text.horizontalOverflow == value)
                {
                    return;
                }

                text.horizontalOverflow = value;
                NotifyChanged();
            }
        }

        /// <summary>垂直截断或溢出策略。</summary>
        public VerticalWrapMode VerticalOverflow
        {
            get => Target.verticalOverflow;
            set
            {
                var text = Target;
                if (text.verticalOverflow == value)
                {
                    return;
                }

                text.verticalOverflow = value;
                NotifyChanged();
            }
        }

        /// <summary>是否使用原生 Best Fit；上下限通过 MinFontSize/MaxFontSize 配置。</summary>
        public bool BestFit
        {
            get => Target.resizeTextForBestFit;
            set
            {
                var text = Target;
                if (text.resizeTextForBestFit == value)
                {
                    return;
                }

                text.resizeTextForBestFit = value;
                NotifyChanged();
            }
        }

        /// <summary>
        /// 自动字号下限，接受非负整数，零值保留原生语义。
        /// 与另一端独立更新，不按中间状态裁剪；调用方应保证完整更新后的范围有效。
        /// </summary>
        public int MinFontSize
        {
            get => Target.resizeTextMinSize;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "自动字号边界必须为非负整数。");
                }

                var text = Target;
                if (text.resizeTextMinSize == value)
                {
                    return;
                }

                text.resizeTextMinSize = value;
                NotifyChanged();
            }
        }

        /// <summary>
        /// 自动字号上限，接受非负整数，零值保留原生语义。
        /// 与另一端独立更新，不按中间状态裁剪；调用方应保证完整更新后的范围有效。
        /// </summary>
        public int MaxFontSize
        {
            get => Target.resizeTextMaxSize;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "自动字号边界必须为非负整数。");
                }

                var text = Target;
                if (text.resizeTextMaxSize == value)
                {
                    return;
                }

                text.resizeTextMaxSize = value;
                NotifyChanged();
            }
        }
    }
}
