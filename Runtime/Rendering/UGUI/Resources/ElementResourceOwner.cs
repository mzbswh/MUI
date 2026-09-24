using System;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>
    /// 将控件的单个资源槽托管给外部生命周期。
    /// 只有槽成功清理后才解除控件的独占写入；同步持有者不进入异步清理路径。
    /// </summary>
    internal sealed partial class ElementResourceOwner<T> : ISynchronousDisposable, IAsyncDisposable where T : class
    {
        private readonly bool synchronous;
        private Action<ElementResourceOwner<T>> released;
        internal ResourceSlot<T> Slot;

        internal ElementResourceOwner(bool synchronous, Action<ElementResourceOwner<T>> released)
        {
            this.synchronous = synchronous;
            this.released = released ?? throw new ArgumentNullException(nameof(released));
        }

        public bool CanDisposeSynchronously => synchronous &&
                    (Slot == null || Slot.CanDisposeSynchronously);

        public void Dispose()
        {
            if (!synchronous)
            {
                throw new InvalidOperationException("异步控件资源槽必须等待释放。");
            }

            Slot?.Dispose();
            ReleaseElement();
        }

        public ValueTask DisposeAsync()
        {
            if (synchronous)
            {
                Dispose();
                return default;
            }

            return DisposeCoreAsync();
        }

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
            sourceOperationChanged = null;
            sourceLifetime = null;
            source = null;
            requestedSource = null;
            Slot = null;
        }
    }
}
