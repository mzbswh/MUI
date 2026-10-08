using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>
    /// 供资源适配器使用的释放包装器；并发 DisposeAsync 共享
    /// 同一个首次完成结果，包含失败。显式幂等重试更新同一责任记录。
    /// 未确认归还时继续持有资源与回调；回调决定归还池、释放引用
    /// 或销毁，此类型不假定 Unity Destroy 语义。
    /// 从正在执行的释放链重入时返回失败，避免等待自身。
    /// 可选释放线程约束在所有权转移前检查，拒绝后仍可由正确线程重试。
    /// </summary>
    public sealed class AcquiredResource<T> : IAcquiredResource<T>, ICleanupResponsibilitySource where T : class
    {
        private readonly object gate = new object();
        private T asset;
        private Func<T, ValueTask> release;
        private bool releaseStarted;

        public AcquiredResource(T asset, Func<T, ValueTask> release, int? releaseThreadId = null,
            bool supportsIdempotentRetry = false, string owner = null)
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
            CleanupResponsibility = new CleanupResponsibility(ReleaseAsync,
                owner ?? "AcquiredResource<" + typeof(T).Name + ">", supportsIdempotentRetry, releaseThreadId);
        }

        /// <summary>同一份归还责任；仅声明支持幂等重试的适配器可显式重试失败。</summary>
        public CleanupResponsibility CleanupResponsibility
        {
            get;
        }

        public T Asset
        {
            get
            {
                lock (gate)
                {
                    if (releaseStarted)
                    {
                        throw new ObjectDisposedException(nameof(AcquiredResource<T>));
                    }

                    return asset;
                }
            }
        }

        public ValueTask DisposeAsync() => CleanupResponsibility.DisposeAsync();

        private async ValueTask ReleaseAsync()
        {
            T value;
            Func<T, ValueTask> callback;
            lock (gate)
            {
                releaseStarted = true;
                value = asset;
                callback = release;
            }

            await callback(value);
            lock (gate)
            {
                asset = null;
                release = null;
            }
        }
    }
}
