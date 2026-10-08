using MUI.Navigation;

namespace MUI.Dialogs
{
    /// <summary>将类型化确认请求交给标准 ViewModel，不承担业务确认后的操作。</summary>
    public sealed class ConfirmationPresenter : Presenter<ConfirmationViewModel, CloseConfirmation, bool>
    {
        /// <summary>在打开阶段设置文案；空请求视为调用错误。</summary>
        protected override void OnOpen(CloseConfirmation args)
        {
            ViewModel.SetRequest(args ?? throw new System.ArgumentNullException(nameof(args)));
        }
    }
}
