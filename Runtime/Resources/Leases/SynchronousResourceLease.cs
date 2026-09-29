using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>
    /// 使用同步回调归还资源，同步调用不创建或读取任务。
    /// 异步接口仅按需提供完成信号；重复释放共享结果，回调只执行一次。
    /// 可选释放线程约束在所有权转移前检查，拒绝后仍可由正确线程重试。
    /// </summary>
    public sealed class SynchronousResourceLease<T> : ISynchronousResourceLease<T>, IResourceLease<T>
        where T : class
    {
        private readonly object gate = new object();
        private readonly int? releaseThreadId;
        private T asset;
        private Action<T> release;
        private ReleaseState state;
        private TaskCompletionSource<bool> disposal;
        private ExceptionDispatchInfo failure;

        public SynchronousResourceLease(T asset, Action<T> release, int? releaseThreadId = null)
        {
            this.asset = asset ?? throw new ArgumentNullException(nameof(asset));
            this.release = release ?? throw new ArgumentNullException(nameof(release));
            if (releaseThreadId.HasValue && releaseThreadId.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(releaseThreadId));
            }

            this.releaseThreadId = releaseThreadId;
        }

        public T Asset
        {
            get
            {
                lock (gate)
                {
                    if (state != ReleaseState.Owned)
                    {
                        throw new ObjectDisposedException(nameof(SynchronousResourceLease<T>));
                    }
                    return asset;
                }
            }
        }

        public void Dispose()
        {
            BeginRelease();
            ExceptionDispatchInfo error;
            lock (gate)
            {
                if (state != ReleaseState.Released)
                {
                    throw new InvalidOperationException("Resource release is already in progress; synchronous disposal cannot wait.");
                }
                error = failure;
            }
            error?.Throw();
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                BeginRelease();
                lock (gate)
                {
                    // 正常同步完成时不需要任务；并发等待或失败才物化兼容信号。
                    if (state == ReleaseState.Released && failure == null)
                    {
                        return default;
                    }
                    if (disposal == null)
                    {
                        disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        if (state == ReleaseState.Released)
                        {
                            CompleteSignal(disposal, failure);
                        }
                    }
                    return new ValueTask(disposal.Task);
                }
            }
            catch (Exception error)
            {
                return new ValueTask(Task.FromException(error));
            }
        }

        private void BeginRelease()
        {
            if (LeaseReleaseContext.Contains(this))
            {
                throw new InvalidOperationException("A release callback cannot release its own resource lease.");
            }

            T value;
            Action<T> callback;
            lock (gate)
            {
                if (state != ReleaseState.Owned)
                {
                    return;
                }

                if (releaseThreadId.HasValue && Thread.CurrentThread.ManagedThreadId != releaseThreadId.Value)
                {
                    throw new InvalidOperationException("Resource release requires its owning thread.");
                }

                state = ReleaseState.Releasing;
                value = asset;
                callback = release;
                asset = null;
                release = null;
            }

            ExceptionDispatchInfo error = null;
            try
            {
                using (LeaseReleaseContext.Enter(this))
                {
                    callback(value);
                }
            }
            catch (Exception exception)
            {
                error = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                lock (gate)
                {
                    // 终态与等待信号一起发布，避免同步调用读到未记录的失败。
                    failure = error;
                    state = ReleaseState.Released;
                    if (disposal != null)
                    {
                        CompleteSignal(disposal, error);
                    }
                }
            }
        }

        private static void CompleteSignal(TaskCompletionSource<bool> completion, ExceptionDispatchInfo error)
        {
            if (error == null)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(error.SourceException);
                // 同步入口可能已经报告失败；兼容等待仍收到同一异常。
                _ = completion.Task.Exception;
            }
        }

        private enum ReleaseState
        {
            Owned,
            Releasing,
            Released
        }
    }
}
