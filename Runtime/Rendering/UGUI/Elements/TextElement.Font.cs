using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class TextElement
    {
        /// <summary>借用字体对象，不加载或销毁资源；调用方须覆盖文字显示期间的字体持有权。</summary>
        public Font Font
        {
            get => Target.font;
            set
            {
                if (!ReferenceEquals(value, null) && value == null)
                {
                    throw new ArgumentException("不能使用已销毁的字体。", nameof(value));
                }

                var text = Target;
                if (fontResources != null)
                {
                    fontResources.SetBorrowed(value);
                    return;
                }

                if (text.font == value)
                {
                    return;
                }

                text.font = value;
                NotifyChanged();
            }
        }
    }
}
