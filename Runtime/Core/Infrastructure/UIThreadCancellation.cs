using System;
using System.Threading;

namespace MUI
{
    /// <summary>将令牌取消路由到注册时的 UI 线程，不阻塞发起取消的线程。</summary>
    internal sealed class UIThreadCancellation : IDisposable
    {
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly SynchronizationContext context = SynchronizationContext.Current;
        private readonly CancellationToken token;
        private readonly CancellationTokenRegistration registration;
        private Action callback;
        private int disposed;
        private bool invoked;

        internal UIThreadCancellation(CancellationToken token, Action callback)
        {
            this.token = token;
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
            registration = token.Register(Signal);
        }

        private void Signal()
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == threadId)
            {
                Invoke();
                return;
            }

            if (context == null)
            {
                // 不能在计时器或提供方线程执行 View 或绑定回调；所有者
                // 仍可在所属线程释放此注册时排空取消工作。
                UIErrors.Report(new InvalidOperationException("Asynchronous UI cancellation requires an owning SynchronizationContext."));
                return;
            }

            try
            {
                context.Post(_ => Invoke(), null);
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private void Invoke()
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                UIErrors.Report(new InvalidOperationException("The UI SynchronizationContext dispatched cancellation to another thread."));
                return;
            }

            if (invoked)
            {
                return;
            }

            invoked = true;
            try
            {
                callback();
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        public void Dispose()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("Dispose UI cancellation registration on its owning thread.");
            }

            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            // 已完成的 await 可能在排队取消回调之前恢复；此时先排空
            // 回调，再使队列中的副本失效，之后才能开始下一次激活。
            if (token.IsCancellationRequested)
            {
                Invoke();
            }

            Interlocked.Exchange(ref disposed, 1);
            registration.Dispose();
            callback = null;
        }
    }
}
