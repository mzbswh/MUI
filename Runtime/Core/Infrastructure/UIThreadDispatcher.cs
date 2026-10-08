using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>捕获 UI 线程的执行上下文，供后台服务提交表现状态。</summary>
    public sealed class UIThreadDispatcher
    {
        private readonly SynchronizationContext context;
        private readonly int threadId;

        private UIThreadDispatcher(SynchronizationContext context, int threadId)
        {
            this.context = context;
            this.threadId = threadId;
        }

        /// <summary>在 UI 线程捕获当前上下文；无调度上下文时拒绝创建。</summary>
        public static UIThreadDispatcher CaptureCurrent()
        {
            var current = SynchronizationContext.Current;
            if (current == null)
            {
                throw new InvalidOperationException("UI thread dispatch requires a SynchronizationContext.");
            }

            return new UIThreadDispatcher(current, Thread.CurrentThread.ManagedThreadId);
        }

        /// <summary>在所属 UI 线程执行；已在该线程时立即执行。执行前检查到取消则跳过委托。</summary>
        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (Thread.CurrentThread.ManagedThreadId == threadId)
            {
                try
                {
                    action();
                    return Task.CompletedTask;
                }
                catch (Exception error)
                {
                    return Task.FromException(error);
                }
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                context.Post(_ =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        completion.TrySetCanceled(cancellationToken);
                        return;
                    }

                    if (Thread.CurrentThread.ManagedThreadId != threadId)
                    {
                        completion.TrySetException(new InvalidOperationException(
                            "The UI SynchronizationContext dispatched work to another thread."));
                        return;
                    }

                    try
                    {
                        action();
                        completion.TrySetResult(true);
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
    }
}
