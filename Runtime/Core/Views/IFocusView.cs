namespace MUI
{
    /// <summary>可选原生选择支持，与 Presenter 焦点通知保持独立。</summary>
    public interface IFocusView : IView
    {
        void SetFocused(bool focused);

        void ConstrainFocus();
    }
}
