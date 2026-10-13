using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    /// <summary>故意忽略清理取消，展示超时后仍由原实例持有资源，直到关闭钩子退出。</summary>
    public sealed class CloseTimeoutPagePresenter : PagePresenter, IAsyncClosePresenter
    {
        public TaskCompletionSource<bool> AllowClose
        {
            get;
        } =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool SawCleanupCancellation
        {
            get; private set;
        }

        public bool Closed
        {
            get; private set;
        }

        public async ValueTask OnCloseAsync(CancellationToken cancellationToken)
        {
            await AllowClose.Task;
            SawCleanupCancellation = cancellationToken.IsCancellationRequested;
        }

        protected override void OnClose()
        {
            Closed = true;
            base.OnClose();
        }
    }
}
