using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI.Loading
{
    /// <summary>
    /// 在创建线程汇总加载操作，并提供延迟显示、加权进度和输入阻挡状态。
    /// 调用方每帧使用选定的非缩放时钟推进 Advance；本类型不执行实际资源加载。
    /// </summary>
    public sealed class LoadingScope : ILoadingSource, IDisposable
    {
        private readonly List<LoadingOperation> operations = new List<LoadingOperation>();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly double displayDelay;
        private readonly int capacity;
        private double elapsed;
        private bool disposed;
        private bool publishing;
        private bool publishPending;
        private long version;

        /// <summary>创建加载操作组。</summary>
        /// <param name="displayDelay">首个操作开始后，加载提示出现前的秒数；不延迟输入阻挡。</param>
        /// <param name="capacity">同时存活的操作上限，必须大于零。</param>
        public LoadingScope(double displayDelay = 0.2, int capacity = 64)
        {
            if (double.IsNaN(displayDelay) || double.IsInfinity(displayDelay) || displayDelay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(displayDelay));
            }

            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.displayDelay = displayDelay;
            this.capacity = capacity;
        }

        /// <summary>完整状态变更通知。订阅者异常被隔离；重入更新会重新发布最新状态。</summary>
        public event Action<LoadingSnapshot> Changed;

        /// <summary>读取当前汇总状态，不推进时钟或获取输入锁；释放后可读取空闲状态。</summary>
        public LoadingSnapshot Snapshot
        {
            get
            {
                RequireThread();
                return Capture();
            }
        }

        /// <summary>
        /// 注册加载提示并将返回令牌交给 owner 托管。调用方应在实际工作结束时提前释放令牌，
        /// owner 的清理提供兜底。提示延迟期间输入阻挡仍立即生效。
        /// </summary>
        /// <param name="owner">令牌所属生命周期，必须尚未结束。</param>
        /// <param name="message">非空提示；汇总状态展示最后加入且仍存活的操作文案。</param>
        /// <param name="weight">有限正数，用于所有操作均有确定进度时的加权平均。</param>
        /// <param name="blockInput">可选输入门；令牌释放时归还本次取得的阻挡凭证。</param>
        public LoadingOperation Begin(LifetimeScope owner, string message, double weight = 1, InputGate blockInput = null)
        {
            RequireAlive();
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (owner.IsEnded)
            {
                throw new ObjectDisposedException(nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Loading message is required.", nameof(message));
            }

            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            if (operations.Count >= capacity)
            {
                throw new InvalidOperationException("Loading operation capacity exhausted.");
            }

            var operation = new LoadingOperation(this, message, weight, blockInput != null);
            if (operations.Count == 0)
            {
                elapsed = 0;
            }

            operations.Add(operation);
            try
            {
                LoadingOperationOwners.Own(owner, operation);
                if (blockInput != null)
                {
                    operation.AttachBlocker(blockInput.Block(message));
                }

                // 获取输入锁会调用外部观察者，回调可能结束 owner 或释放整个操作组。
                if (!operation.IsActive || owner.IsEnded)
                {
                    throw new OperationCanceledException("Loading operation owner ended while acquiring input.");
                }

                Publish();
                if (!operation.IsActive || owner.IsEnded)
                {
                    throw new OperationCanceledException("Loading operation ended during publication.");
                }

                return operation;
            }
            catch (Exception failure)
            {
                try
                {
                    operation.Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(failure, cleanup);
                }

                throw;
            }
        }

        /// <summary>推进提示延迟。由服务所有者每帧调用一次，不使用 Unity 的缩放时间。</summary>
        /// <param name="unscaledDeltaTime">非负有限秒数。</param>
        public void Advance(double unscaledDeltaTime)
        {
            RequireAlive();
            if (double.IsNaN(unscaledDeltaTime) || double.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            if (operations.Count == 0 || elapsed >= displayDelay)
            {
                return;
            }

            elapsed = unscaledDeltaTime >= displayDelay - elapsed ? displayDelay : elapsed + unscaledDeltaTime;
            if (elapsed >= displayDelay)
            {
                Publish();
            }
        }

        internal bool Report(LoadingOperation operation, double? progress)
        {
            RequireThread();
            if (!operation.IsActive || disposed)
            {
                return false;
            }

            if (progress.HasValue && (double.IsNaN(progress.Value) || double.IsInfinity(progress.Value) ||
                progress < 0 || progress > 1))
            {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }

            if (operation.Progress == progress)
            {
                return true;
            }

            operation.Progress = progress;
            Publish();
            return true;
        }

        internal void Release(LoadingOperation operation)
        {
            RequireThread();
            operations.Remove(operation);
            if (operations.Count == 0)
            {
                elapsed = 0;
            }

            try
            {
                operation.ReleaseBlocker();
            }
            finally
            {
                if (!disposed)
                {
                    Publish();
                }
            }
        }

        private LoadingSnapshot Capture()
        {
            var count = operations.Count;
            if (count == 0)
            {
                return new LoadingSnapshot(0, 0, false, null, string.Empty);
            }

            var blocking = 0;
            var determinate = true;
            var maxWeight = 0d;
            foreach (var operation in operations)
            {
                if (operation.BlocksInput)
                {
                    ++blocking;
                }

                if (!operation.Progress.HasValue)
                {
                    determinate = false;
                }

                maxWeight = Math.Max(maxWeight, operation.Weight);
            }

            double? progress = null;
            if (determinate)
            {
                var numerator = 0d;
                var denominator = 0d;
                foreach (var operation in operations)
                {
                    // 先按最大权重归一化，避免多个合法的大权重求和溢出。
                    var weight = operation.Weight / maxWeight;
                    denominator += weight;
                    numerator += weight * operation.Progress.Value;
                }

                progress = Math.Max(0, Math.Min(1, numerator / denominator));
            }

            return new LoadingSnapshot(count, blocking, elapsed >= displayDelay, progress, operations[count - 1].Message);
        }

        // 观察者允许重入修改操作。代际变化后停止发送旧快照，下一轮发送最新快照。
        // 限制重入轮次，避免业务观察者相互更新导致主线程永不返回。
        private void Publish()
        {
            ++version;
            publishPending = true;
            if (publishing)
            {
                return;
            }

            publishing = true;
            try
            {
                var passes = 0;
                while (publishPending)
                {
                    if (++passes > 32)
                    {
                        UIErrors.Report(new InvalidOperationException("Loading state did not stabilize after 32 publications."));
                        break;
                    }

                    publishPending = false;
                    var currentVersion = version;
                    var snapshot = Capture();
                    var handlers = Changed;
                    if (handlers == null)
                    {
                        continue;
                    }

                    foreach (Action<LoadingSnapshot> handler in handlers.GetInvocationList())
                    {
                        if (version != currentVersion)
                        {
                            break;
                        }

                        try
                        {
                            handler(snapshot);
                        }
                        catch (Exception error)
                        {
                            UIErrors.Report(error);
                        }
                    }
                }
            }
            finally
            {
                publishing = false;
                publishPending = false;
                // 释放可能发生在观察者内部；空闲状态发布完成后再清除订阅。
                if (disposed)
                {
                    Changed = null;
                }
            }
        }

        /// <summary>
        /// 在所属线程释放所有操作和输入阻挡，发布空闲状态后解除观察者引用。
        /// 不取消或等待业务任务；实际任务的生命周期由调用方管理。
        /// </summary>
        public void Dispose()
        {
            RequireThread();
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (var operation in operations.ToArray())
            {
                try
                {
                    operation.Dispose();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }

            Publish();
        }

        internal void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Loading scope requires its owning UI thread.");
            }
        }

        private void RequireAlive()
        {
            RequireThread();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LoadingScope));
            }
        }
    }
}
