using System;
using System.Collections.Generic;

namespace MUI
{
    /// <summary>文本选项与选择索引的公共契约；原生图片、占位和弹出布局仍由具体后端提供。</summary>
    public interface IDropdownElement : IElement
    {
        /// <summary>有效的用户选择；先通知 Value，再发布事件，模型赋值不发布。</summary>
        event Action SelectionChanged;

        /// <summary>原生选择索引；空选项及 TMP 占位项沿用各后端的原生值语义。</summary>
        int Value
        {
            get; set;
        }

        /// <summary>赋值时复制文本选项；读取返回快照。</summary>
        IReadOnlyList<string> Options
        {
            get; set;
        }

        bool Interactable
        {
            get; set;
        }
    }
}
