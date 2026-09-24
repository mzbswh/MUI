using System;

namespace MUI.Dialogs
{
    /// <summary>单按钮提示框的不可变请求；文案由项目完成本地化后传入。</summary>
    public sealed class AlertRequest
    {
        /// <summary>创建提示内容；确认按钮仅表示用户已阅读，不执行隐含业务操作。</summary>
        public AlertRequest(string title, string message, string acknowledgeLabel = "确定")
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            AcknowledgeLabel = acknowledgeLabel ?? throw new ArgumentNullException(nameof(acknowledgeLabel));
        }

        /// <summary>标题。</summary>
        public string Title
        {
            get;
        }

        /// <summary>正文。</summary>
        public string Message
        {
            get;
        }

        /// <summary>已阅读按钮的文案。</summary>
        public string AcknowledgeLabel
        {
            get;
        }
    }
}
