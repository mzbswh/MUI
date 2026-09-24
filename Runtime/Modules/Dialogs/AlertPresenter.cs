using MUI.Navigation;

namespace MUI.Dialogs
{
    /// <summary>将提示请求交给标准 ViewModel，不承担用户阅读后的业务操作。</summary>
    public sealed class AlertPresenter : Presenter<AlertViewModel, AlertRequest, Unit>
    {
        /// <summary>在打开阶段设置文案；空请求视为调用错误。</summary>
        protected override void OnOpen(AlertRequest args)
        {
            ViewModel.SetRequest(args ?? throw new System.ArgumentNullException(nameof(args)));
        }
    }
}
