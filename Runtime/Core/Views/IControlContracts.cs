using System;

namespace MUI
{
    /// <summary>按钮的后端无关绑定契约。</summary>
    public interface IButtonElement : IElement
    {
        bool Interactable { get; set; }
        event Action Clicked;
    }

    /// <summary>值控件的通用契约；模型写入仅通知属性，不发布用户操作事件。</summary>
    public interface IValueElement<T> : IElement
    {
        T Value { get; set; }
        bool Interactable { get; set; }
        event Action ValueChanged;
    }
}
