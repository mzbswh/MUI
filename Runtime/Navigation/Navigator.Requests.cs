using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private TaskCompletionSource<bool> requestsDrained;

        // UI 线程上的请求计数覆盖排队、准备、确认与回滚。
        // 每个已接受请求必须在最外层 finally 中且仅退出计数一次。
        // 同步模式只维护计数；排空任务仅供允许异步的宿主退出时等待。
        private void BeginNavigationRequest()
        {
            if (pending == 0 && Mode == LifetimeMode.AsyncAllowed)
            {
                requestsDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            pending++;
        }

        private void EndNavigationRequest()
        {
            pending--;
            if (pending == 0 && Mode == LifetimeMode.AsyncAllowed)
            {
                requestsDrained.TrySetResult(true);
            }
        }

        private Task WaitForNavigationRequestsAsync()
        {
            // 同步宿主通过直接请求计数预检退出，不能读取或创建异步排空信号。
            RequireAsyncNavigation();
            return pending == 0 ? Task.CompletedTask : requestsDrained.Task;
        }
    }
}
