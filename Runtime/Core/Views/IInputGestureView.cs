using System;

namespace MUI
{
    /// <summary>绑定切换和输入资格失效时同步结束旧手势，不取消已接纳的业务命令。</summary>
    public interface IInputGestureView : IInputView
    {
        /// <summary>内部捕获清理通知；适配器应取消捕获，不派发点击、提交或投放业务事件。</summary>
        event Action InputGesturesInvalidated;

        /// <summary>使当前手势失效；新手势仍按当前输入资格接纳。</summary>
        void InvalidateInputGestures();
    }
}
