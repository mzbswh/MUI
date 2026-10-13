using System;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    /// <summary>可延迟释放的示例模型，用于观察换绑与关闭的资源顺序。</summary>
    public sealed class RebindPageViewModel : PageViewModel, IAsyncDisposable
    {
        public bool DelayRelease
        {
            get; set;
        }

        public int ReleaseCount
        {
            get; private set;
        }

        public TaskCompletionSource<bool> ReleaseStarted
        {
            get;
        } =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowRelease
        {
            get;
        } =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask DisposeAsync()
        {
            ReleaseStarted.TrySetResult(true);
            if (DelayRelease)
            {
                await AllowRelease.Task;
            }

            ++ReleaseCount;
        }
    }
}
