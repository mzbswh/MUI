using MUI.Navigation;

namespace MUI.Dialogs
{
    /// <summary>
    /// 标准单按钮提示框的表现状态，绑定将已阅读命令连接到本次界面结果。
    /// 不持有业务操作，也不直接调用导航服务。
    /// </summary>
    public sealed class AlertViewModel : ViewModel
    {
        private string title = string.Empty;
        private string message = string.Empty;
        private string acknowledgeLabel = string.Empty;

        /// <summary>创建同步完成结果的命令；必须在有效的界面命令上下文中执行。</summary>
        public AlertViewModel()
        {
            Acknowledge = new SynchronousCommand(context => context.Complete(Unit.Value));
        }

        /// <summary>标题。</summary>
        public string Title
        {
            get => title;
            private set => SetProperty(ref title, value);
        }

        /// <summary>提示正文。</summary>
        public string Message
        {
            get => message;
            private set => SetProperty(ref message, value);
        }

        /// <summary>已阅读按钮文案。</summary>
        public string AcknowledgeLabel
        {
            get => acknowledgeLabel;
            private set => SetProperty(ref acknowledgeLabel, value);
        }

        /// <summary>以 Unit 完成本次界面结果，不在此处执行业务操作。</summary>
        public ISynchronousUICommand Acknowledge
        {
            get;
        }

        /// <summary>打开阶段填充本次请求文案，之后由属性通知驱动控件刷新。</summary>
        internal void SetRequest(AlertRequest request)
        {
            Title = request.Title;
            Message = request.Message;
            AcknowledgeLabel = request.AcknowledgeLabel;
        }
    }
}
