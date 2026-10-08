using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private TaskCompletionSource<bool> requestsDrained;

        /// <summary>结束短提交区；先清除本次持有标记，异常收尾和 finally 可安全重复调用。</summary>
        private void ReleaseNavigationQueue(ref bool acquired)
        {
            if (acquired)
            {
                acquired = false;
                requests.Release();
            }
        }

        // UI 线程上的请求计数覆盖排队、准备、确认与回滚。
        // 每个已接受请求必须在最外层 finally 中且仅退出计数一次。
        // 退出等待同一计数归零，不把准备期间持有导航队列许可。
        private void BeginNavigationRequest()
        {
            if (pending == 0)
            {
                requestsDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            pending++;
        }

        private void EndNavigationRequest()
        {
            pending--;
            if (pending == 0)
            {
                requestsDrained.TrySetResult(true);
            }
        }

        private Task WaitForNavigationRequestsAsync()
        {
            return pending == 0 ? Task.CompletedTask : requestsDrained.Task;
        }
    }
}
