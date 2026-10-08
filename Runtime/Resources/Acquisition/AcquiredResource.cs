using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>
    /// 供资源适配器使用的恰好一次释放包装器；并发释放者共享
    /// 同一个完成结果，包含失败。回调决定归还池、释放引用
    /// 或销毁，此类型不假定 Unity Destroy 语义。
    /// 从正在执行的释放链重入时返回失败，避免等待自身。
    /// 可选释放线程约束在所有权转移前检查，拒绝后仍可由正确线程重试。
    /// </summary>
    public sealed class AcquiredResource<T> : IAcquiredResource<T> where T : class
    {
        private readonly object gate = new object();
        private readonly int? releaseThreadId;
        private T asset;
        private Func<T, ValueTask> release;
        private TaskCompletionSource<bool> disposal;

        public AcquiredResource(T asset, Func<T, ValueTask> release, int? releaseThreadId = null)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            this.release = release ?? throw new ArgumentNullException(nameof(release));
            if (releaseThreadId.HasValue && releaseThreadId.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(releaseThreadId));
            }

            this.asset = asset;
            this.releaseThreadId = releaseThreadId;
        }

        public T Asset
        {
            get
            {
                lock (gate)
                {
                    if (disposal != null)
                    {
                        throw new ObjectDisposedException(nameof(AcquiredResource<T>));
                    }

                    return asset;
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (ResourceReleaseContext.Contains(this))
            {
                return new ValueTask(Task.FromException(new InvalidOperationException("A resource release callback cannot await its own resource release.")));
            }

            T value;
            Func<T, ValueTask> callback;
            TaskCompletionSource<bool> completion;
            lock (gate)
            {
                if (disposal != null)
                {
                    return new ValueTask(disposal.Task);
                }

                if (releaseThreadId.HasValue && Thread.CurrentThread.ManagedThreadId != releaseThreadId.Value)
                {
                    return new ValueTask(Task.FromException(new InvalidOperationException("Resource release requires its owning thread.")));
                }

                completion = disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                value = asset;
                callback = release;
                asset = null;
                release = null;
            }

            _ = ReleaseAsync(value, callback, completion);
            return new ValueTask(completion.Task);
        }

        private async Task ReleaseAsync(T value, Func<T, ValueTask> callback, TaskCompletionSource<bool> completion)
        {
            try
            {
                using (ResourceReleaseContext.Enter(this))
                {
                    await callback(value);
                }

                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }
    }
}
