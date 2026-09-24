using MUI.Navigation;

namespace MUI.Dialogs
{
    /// <summary>
    /// 标准确认框的表现状态。Presenter 注入请求，生成绑定将确认与取消命令连接到本次界面结果。
    /// 不持有待确认的业务操作，也不直接调用导航服务。
    /// </summary>
    public sealed class ConfirmationViewModel : ViewModel
    {
        private string title = string.Empty;
        private string message = string.Empty;
        private string confirmLabel = string.Empty;
        private string cancelLabel = string.Empty;

        /// <summary>创建两个完成结果的命令；必须在有效的界面命令上下文中执行。</summary>
        public ConfirmationViewModel()
        {
            Confirm = new SynchronousCommand(context => context.Complete(true));
            Cancel = new SynchronousCommand(context => context.Complete(false));
        }

        /// <summary>标题。</summary>
        public string Title
        {
            get => title; private set => SetProperty(ref title, value);
        }

        /// <summary>需要用户确认的说明。</summary>
        public string Message
        {
            get => message; private set => SetProperty(ref message, value);
        }

        /// <summary>确认按钮文案。</summary>
        public string ConfirmLabel
        {
            get => confirmLabel; private set => SetProperty(ref confirmLabel, value);
        }

        /// <summary>取消按钮文案。</summary>
        public string CancelLabel
        {
            get => cancelLabel; private set => SetProperty(ref cancelLabel, value);
        }

        /// <summary>以 true 完成本次界面结果，不在此处执行业务操作。</summary>
        public ISynchronousUICommand Confirm
        {
            get;
        }

        /// <summary>以 false 完成本次界面结果。</summary>
        public ISynchronousUICommand Cancel
        {
            get;
        }

        /// <summary>打开阶段填充本次请求文案，之后由属性通知驱动控件刷新。</summary>
        internal void SetRequest(CloseConfirmation request)
        {
            Title = request.Title;
            Message = request.Message;
            ConfirmLabel = request.ConfirmLabel;
            CancelLabel = request.CancelLabel;
        }
    }
}
