using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public enum CloseDecisionKind
    {
        Allow,
        Deny,
        NeedsConfirmation
    }

    public sealed class CloseConfirmation
    {
        public CloseConfirmation(string title, string message, string confirmLabel = "Confirm", string cancelLabel = "Cancel")
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ConfirmLabel = confirmLabel ?? throw new ArgumentNullException(nameof(confirmLabel));
            CancelLabel = cancelLabel ?? throw new ArgumentNullException(nameof(cancelLabel));
        }

        public string Title
        {
            get;
        }

        public string Message
        {
            get;
        }

        public string ConfirmLabel
        {
            get;
        }

        public string CancelLabel
        {
            get;
        }
    }

    public readonly struct CloseDecision
    {
        private CloseDecision(CloseDecisionKind kind, CloseConfirmation confirmation)
        {
            Kind = kind;
            Confirmation = confirmation;
        }

        public CloseDecisionKind Kind
        {
            get;
        }

        public CloseConfirmation Confirmation
        {
            get;
        }

        public static CloseDecision Allow => new CloseDecision(CloseDecisionKind.Allow, null);

        public static CloseDecision Deny => new CloseDecision(CloseDecisionKind.Deny, null);

        public static CloseDecision Confirm(CloseConfirmation confirmation) => new CloseDecision(CloseDecisionKind.NeedsConfirmation, confirmation ?? throw new ArgumentNullException(nameof(confirmation)));
    }

    public readonly struct CloseContext
    {
        internal CloseContext(ViewHandle handle, DismissReason reason, bool isCompletion)
        {
            Handle = handle;
            Reason = reason;
            IsCompletion = isCompletion;
        }

        public ViewHandle Handle
        {
            get;
        }

        public DismissReason Reason
        {
            get;
        }

        public bool IsCompletion
        {
            get;
        }
    }

    /// <summary>可选 Presenter 能力；影响关闭许可的数据变化时需递增 CloseVersion。</summary>
    public interface ICloseGuard
    {
        long CloseVersion
        {
            get;
        }

        ValueTask<CloseDecision> CanCloseAsync(CloseContext context, CancellationToken cancellationToken);
    }

    /// <summary>独立同步关闭守卫；立即允许或拒绝，业务需先自行完成交互确认再重新请求关闭。</summary>
    public interface ISynchronousCloseGuard
    {
        long CloseVersion
        {
            get;
        }

        bool CanClose(CloseContext context);
    }

    /// <summary>可通过 Navigator 打开对话框，必须遵守取消并释放所持有的对话框。</summary>
    public interface ICloseConfirmationService
    {
        ValueTask<bool> ConfirmAsync(CloseContext context, CloseConfirmation confirmation, CancellationToken cancellationToken);
    }
}
