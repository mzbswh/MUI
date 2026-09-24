using System;
using System.Threading;

namespace MUI
{
    public sealed partial class Lifetime
    {
        private int synchronousOperations;

        /// <summary>
        /// 登记并在当前线程执行同步操作；两种生命周期模式均支持。
        /// 操作退出前，Dispose 拒绝同步释放；只有允许异步的模式可等待其退出。
        /// 不调用异步委托，不调度后台工作，也不等待其他任务。
        /// </summary>
        public T Run<T>(Func<CancellationToken, T> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (Mode == LifetimeMode.Synchronous)
            {
                lock (gate)
                {
                    ThrowIfEnded();
                    ++synchronousOperations;
                }
                try
                {
                    token.ThrowIfCancellationRequested();
                    return operation(token);
                }
                finally
                {
                    lock (gate)
                    {
                        --synchronousOperations;
                    }
                }
            }

            // 完成信号仅供可能已经进入的异步清理观察，不承载业务结果。
            var completion = NewCompletion();
            lock (gate)
            {
                ThrowIfEnded();
                operations.Add(completion.Task);
            }

            try
            {
                token.ThrowIfCancellationRequested();
                return operation(token);
            }
            finally
            {
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
