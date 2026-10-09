using System;
using System.Threading;

namespace MUI
{
    public sealed partial class LifetimeScope
    {
        /// <summary>
        /// 登记并在当前线程执行同步操作。
        /// DisposeAsync 等待操作退出后再归还资源。
        /// 不调用异步委托，不调度后台工作，也不等待其他任务。
        /// </summary>
        public T Run<T>(Func<CancellationToken, T> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            // 完成信号仅供可能已经进入的异步清理观察，不承载业务结果。
            var completion = NewCompletion();
            lock (gate)
            {
                ThrowIfEnded();
                operations.Add(completion.Task);
            }

            // 同步委托也属于被跟踪的操作，不能在回调内等待销毁自身。
            var previous = CurrentOperation.Value;
            var frame = new OperationFrame { Owner = this, Parent = previous };
            CurrentOperation.Value = frame;
            try
            {
                token.ThrowIfCancellationRequested();
                return operation(token);
            }
            finally
            {
                Volatile.Write(ref frame.Active, false);
                frame.Owner = null;
                CurrentOperation.Value = previous;
                lock (gate)
                {
                    operations.Remove(completion.Task);
                }

                // 业务异常由同步调用者接收，清理等待者只关心操作已退出。
                completion.TrySetResult(true);
            }
        }

        /// <summary>登记不返回结果的同步操作；错误直接交由调用者处理。</summary>
        public void Run(Action<CancellationToken> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            Run(token =>
            {
                operation(token);
                return true;
            });
        }
    }
}
