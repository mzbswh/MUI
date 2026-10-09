using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>一个资源身份的共享准备。每个请求独立撤销资格，驻留提交后由批次作用域持有。</summary>
        private sealed class PreloadEntry : IDisposable
        {
            private readonly object gate = new object();
            private readonly CancellationTokenSource stop = new CancellationTokenSource();
            private readonly TaskCompletionSource<PreloadOutcome> completion =
                new TaskCompletionSource<PreloadOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<Exception> cancellationFinished =
                new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int consumers;
            private bool abandoned;
            private bool completed;

            internal CancellationToken Token => stop.Token;

            internal Consumer Join(CancellationToken token)
            {
                bool joined;
                lock (gate)
                {
                    joined = !completed && !abandoned;
                    if (joined)
                    {
                        ++consumers;
                    }
                }
                return new Consumer(this, joined, token);
            }

            internal async Task<PreloadOutcome> ObserveAsync(Consumer consumer, CancellationToken token, bool reused)
            {
                try
                {
                    var result = await consumer.WaitAsync(completion.Task, token);
                    return new PreloadOutcome(result.Status, result.Error, reused);
                }
                catch (OperationCanceledException)
                {
                    return new PreloadOutcome(reused ? PreloadStatus.WaitCancelled : PreloadStatus.Cancelled,
                        reusedReservation: reused);
                }
                finally
                {
                    consumer.Dispose();
                }
            }

            internal bool TryRetain(Action adopt)
            {
                lock (gate)
                {
                    if (abandoned)
                    {
                        return false;
                    }
                    // 锁内仅登记框架持有对象；驻留提交与最后一个请求的取消共同决定准入资格。
                    adopt();
                    completed = true;
                    completion.TrySetResult(new PreloadOutcome(PreloadStatus.Ready));
                    return true;
                }
            }

            /// <summary>封闭请求资格后等待已经开始的取消回调；不在锁内执行或等待项目回调。</summary>
            internal Task<Exception> EndAdmission()
            {
                lock (gate)
                {
                    completed = true;
                    return abandoned ? cancellationFinished.Task : Task.FromResult<Exception>(null);
                }
            }

            internal void Complete(PreloadOutcome result) => completion.TrySetResult(result);

            public void Dispose() => stop.Dispose();

            private void Leave(bool cancelled, TaskCompletionSource<bool> cancellation = null)
            {
                var cancelPreparation = false;
                lock (gate)
                {
                    --consumers;
                    if (cancelled && consumers == 0 && !completed && !abandoned)
                    {
                        abandoned = true;
                        cancelPreparation = true;
                    }
                }
                // 先撤销准备资格，再通知观察者；项目取消回调不延迟本次等待的取消结果。
                cancellation?.TrySetResult(true);
                if (!cancelPreparation)
                {
                    return;
                }
                Exception failure = null;
                try
                {
                    stop.Cancel(throwOnFirstException: false);
                }
                catch (Exception error)
                {
                    failure = error;
                }
                finally
                {
                    cancellationFinished.TrySetResult(failure);
                }
            }

            internal sealed class Consumer : IDisposable
            {
                private const int Detached = 0;
                private const int Active = 1;
                private const int Cancelling = 2;
                private readonly PreloadEntry entry;
                private readonly bool participates;
                private readonly CancellationTokenRegistration cancellation;
                private readonly TaskCompletionSource<bool> cancelled;
                private readonly TaskCompletionSource<bool> cancellationFinished;
                private int state = Active;

                internal Consumer(PreloadEntry entry, bool joined, CancellationToken token)
                {
                    this.entry = entry;
                    participates = joined;
                    if (token.CanBeCanceled)
                    {
                        cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        cancellationFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        cancellation = token.Register(state => ((Consumer)state).Cancel(), this);
                    }
                }

                internal async Task<PreloadOutcome> WaitAsync(Task<PreloadOutcome> result, CancellationToken token)
                {
                    if (cancelled != null && cancelled.Task.IsCompleted)
                    {
                        throw new OperationCanceledException(token);
                    }
                    if (cancelled == null)
                    {
                        token.ThrowIfCancellationRequested();
                        return await result;
                    }
                    if (await Task.WhenAny(result, cancelled.Task) != result)
                    {
                        throw new OperationCanceledException(token);
                    }
                    return await result;
                }

                public void Dispose()
                {
                    // 先夺取资格再解除登记；回调已接管时等待其收尾，
                    // 避免 Unity 的同步登记释放阻塞 UI。
                    var previous = Interlocked.CompareExchange(ref state, Detached, Active);
                    if (previous == Active)
                    {
                        if (participates)
                        {
                            entry.Leave(false);
                        }
                        cancellation.Dispose();
                    }
                    else if (previous == Cancelling)
                    {
                        _ = ReleaseCancellationAsync();
                    }
                }

                private void Cancel()
                {
                    if (Interlocked.CompareExchange(ref state, Cancelling, Active) != Active)
                    {
                        return;
                    }
                    try
                    {
                        if (participates)
                        {
                            entry.Leave(true, cancelled);
                        }
                        else
                        {
                            // 已放弃或已就绪的准备不再接纳资格，但观察者仍可独立取消等待。
                            cancelled.TrySetResult(true);
                        }
                    }
                    finally
                    {
                        cancellationFinished.TrySetResult(true);
                    }
                }

                private async Task ReleaseCancellationAsync()
                {
                    await cancellationFinished.Task;
                    cancellation.Dispose();
                    Interlocked.Exchange(ref state, Detached);
                }
            }
        }
    }
}
