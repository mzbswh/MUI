using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    public sealed partial class PagedList<T, TCursor>
    {
        private readonly SynchronizationContext context = SynchronizationContext.Current;
        private Exception dispatchFailure;
        private bool ownerCallback;

        /// <summary>将同步的提交或通知明确调度到创建线程，不依赖项目异步回调保留当前上下文。</summary>
        private Task<TResult> OnOwnerAsync<TResult>(Func<TResult> action)
        {
            if (Thread.CurrentThread.ManagedThreadId == thread)
            {
                try
                {
                    return Task.FromResult(InvokeOwnerAction(action));
                }
                catch (Exception error)
                {
                    return Task.FromException<TResult>(error);
                }
            }

            var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                context.Post(_ =>
                {
                    try
                    {
                        // 不正确的同步上下文不能通过再次 Post 无限重试或绕过线程归属。
                        RequireThread();
                        completion.TrySetResult(InvokeOwnerAction(action));
                    }
                    catch (Exception error)
                    {
                        completion.TrySetException(error);
                    }
                }, null);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }

            return completion.Task;
        }

        private TResult InvokeOwnerAction<TResult>(Func<TResult> action)
        {
            var previous = ownerCallback;
            ownerCallback = true;
            try
            {
                return action();
            }
            finally
            {
                ownerCallback = previous;
            }
        }
    }
}
