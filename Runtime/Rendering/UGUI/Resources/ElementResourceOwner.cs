using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>
    /// 将控件的单个资源槽托管给外部生命周期。
    /// 直接对象与资源键写入共用此槽；只有清理成功后才能配置新的拥有者。
    /// </summary>
    internal sealed partial class ElementResourceOwner<T> : IAsyncDisposable, ICleanupResponsibilitySource where T : class
    {
        private Action<ElementResourceOwner<T>> released;
        private readonly UIErrorContext diagnosticContext;
        internal ResourceSlot<T> Slot;

        internal ElementResourceOwner(Action<ElementResourceOwner<T>> released)
        {
            this.released = released ?? throw new ArgumentNullException(nameof(released));
            diagnosticContext = UIErrors.CurrentContext;
            CleanupResponsibility = new CleanupResponsibility(DisposeCoreAsync,
                "ElementResourceOwner<" + typeof(T).Name + ">", true, Thread.CurrentThread.ManagedThreadId);
        }

        public CleanupResponsibility CleanupResponsibility
        {
            get;
        }

        private IDisposable BeginResourcePhase(string phase) =>
            UIErrors.BeginOwnedPhase(diagnosticContext, "Resource", phase);

        public ValueTask DisposeAsync() => CleanupResponsibility.DisposeAsync();

        private async ValueTask DisposeCoreAsync()
        {
            if (Slot != null)
            {
                try
                {
                    await Slot.DisposeAsync();
                }
                catch (Exception) when (Slot.IsCleanupConfirmed)
                {
                    // 子责任均已确认后，外层只执行尚未完成的解除持有；不改写槽的首次失败。
                }
            }

            ReleaseElement();
        }

        private void ReleaseElement()
        {
            var callback = released;
            if (callback == null)
            {
                return;
            }

            callback(this);
            released = null;
            // 槽已成功清理，结束后不再通过通知闭包或槽委托保留控件和激活上下文。
            sourceChanged = null;
            sourceStateChanged = null;
            sourceOperationChanged = null;
            sourceLifetime = null;
            source = null;
            requestedSource = null;
            Slot = null;
        }
    }
}
