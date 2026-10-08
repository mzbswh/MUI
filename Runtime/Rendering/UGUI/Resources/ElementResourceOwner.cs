using System;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>
    /// 将控件的单个资源槽托管给外部生命周期。
    /// 直接对象与资源键写入共用此槽；只有清理成功后才能配置新的拥有者。
    /// </summary>
    internal sealed partial class ElementResourceOwner<T> : IAsyncDisposable where T : class
    {
        private Action<ElementResourceOwner<T>> released;
        internal ResourceSlot<T> Slot;

        internal ElementResourceOwner(Action<ElementResourceOwner<T>> released)
        {
            this.released = released ?? throw new ArgumentNullException(nameof(released));
        }

        public ValueTask DisposeAsync() => DisposeCoreAsync();

        private async ValueTask DisposeCoreAsync()
        {
            if (Slot != null)
            {
                await Slot.DisposeAsync();
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
