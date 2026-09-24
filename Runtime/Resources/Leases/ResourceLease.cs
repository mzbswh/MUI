using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>
    /// 供资源适配器使用的恰好一次释放包装器；并发释放者共享
    /// 同一个完成结果，包含失败。回调决定归还池、释放引用
    /// 或销毁，此类型不假定 Unity Destroy 语义。
    /// 从正在执行的释放链重入时返回失败，避免等待自身。
    /// </summary>
    public sealed class ResourceLease<T> : IResourceLease<T> where T : class
    {
        private readonly object gate = new object();
        private T asset;
        private Func<T, ValueTask> release;
        private TaskCompletionSource<bool> disposal;

        public ResourceLease(T asset, Func<T, ValueTask> release)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            this.release = release ?? throw new ArgumentNullException(nameof(release));
            this.asset = asset;
        }

        public T Asset
        {
            get
            {
                lock (gate)
                {
                    if (disposal != null)
                    {
                        throw new ObjectDisposedException(nameof(ResourceLease<T>));
                    }

                    return asset;
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (LeaseReleaseContext.Contains(this))
            {
                return new ValueTask(Task.FromException(new InvalidOperationException("A resource release callback cannot await its own lease release.")));
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
                using (LeaseReleaseContext.Enter(this))
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
