using System;

namespace MUI
{
    /// <summary>渲染器输入能力，有效输入包含局部与宿主限制。</summary>
    public interface IInputView : IView
    {
        event Action InputStateChanged;

        InputGate InputGate
        {
            get;
        }

        bool IsInputEnabled
        {
            get;
        }
    }
}
