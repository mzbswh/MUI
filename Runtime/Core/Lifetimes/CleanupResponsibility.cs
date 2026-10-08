using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 一次清理责任。重复释放观察首次结果；只有适配器明确声明幂等性时才允许显式重试。
    /// 失败不意味着尚未释放，也不丢弃资源回调；未确认的责任由 CleanupRegistry 继续持有。
    /// </summary>
    public sealed class CleanupResponsibility : IAsyncDisposable
    {
        private static readonly AsyncLocal<ReleaseFrame> currentRelease = new AsyncLocal<ReleaseFrame>();
        private readonly object gate = new object();
        private readonly bool supportsIdempotentRetry;
        private readonly int? releaseThreadId;
        private Func<ValueTask> release;
        private Task firstAttempt;
        private Task currentAttempt;
        private CleanupResponsibilityState state;
        private Exception failure;
        private UIErrorContext context;
        private long attempts;

        public CleanupResponsibility(Func<ValueTask> release, string owner,
            bool supportsIdempotentRetry = false, int? releaseThreadId = null)
        {
            this.release = release ?? throw new ArgumentNullException(nameof(release));
            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("A cleanup responsibility requires an owner label.", nameof(owner));
            }

            if (releaseThreadId.HasValue && releaseThreadId.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(releaseThreadId));
            }

            Id = Guid.NewGuid();
            Owner = owner.Length > 256 ? owner.Substring(0, 256) : owner;
            this.supportsIdempotentRetry = supportsIdempotentRetry;
            this.releaseThreadId = releaseThreadId;
            context = UIErrors.CurrentContext;
        }

        public Guid Id
        {
            get;
        }

        public string Owner
        {
            get;
        }

        // 首次登记到账本前确定身份；在途、失败及重试期间不改写，账本锁内无需再取得责任锁。
        internal Guid HostId => context.HostId;

        internal long ViewId => context.ViewId;

        /// <summary>只读值快照，不调用资源或适配器的属性。</summary>
        public CleanupResponsibilitySnapshot CaptureSnapshot()
        {
            lock (gate)
            {
                return new CleanupResponsibilitySnapshot(Id, Owner, state, failure, context,
                    attempts, supportsIdempotentRetry && state == CleanupResponsibilityState.Failed);
            }
        }

        /// <summary>第一次尝试的共享结果；重试成功也不改写已交付的失败任务。</summary>
        public ValueTask DisposeAsync() => new ValueTask(BeginAttempt(false));

        /// <summary>显式安全重试；在途调用合并，确认成功后不再次调用适配器。</summary>
        public Task RetryAsync() => BeginAttempt(true);

        private Task BeginAttempt(bool retry)
        {
            for (var frame = currentRelease.Value; frame != null; frame = frame.Parent)
            {
                if (Volatile.Read(ref frame.Active) && ReferenceEquals(frame.Owner, this))
                {
                    return Task.FromException(new InvalidOperationException("A cleanup callback cannot await its own responsibility."));
                }
            }

            TaskCompletionSource<bool> completion;
            Func<ValueTask> callback;
            lock (gate)
            {
                if (!retry && firstAttempt != null)
                {
                    return firstAttempt;
                }

                if (retry)
                {
                    if (state == CleanupResponsibilityState.Completed)
                    {
                        return currentAttempt;
                    }

                    if (!supportsIdempotentRetry || firstAttempt == null)
                    {
                        return Task.FromException(new InvalidOperationException("This responsibility is not eligible for an explicit idempotent retry."));
                    }

                    if (state == CleanupResponsibilityState.Pending)
                    {
                        return currentAttempt;
                    }
                }

                // 线程拒绝不接管本次尝试，正确线程仍可执行原始释放或显式重试。
                if (releaseThreadId.HasValue && Thread.CurrentThread.ManagedThreadId != releaseThreadId.Value)
                {
                    return Task.FromException(new InvalidOperationException("Cleanup requires its owning thread."));
                }

                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                currentAttempt = completion.Task;
                if (firstAttempt == null)
                {
                    firstAttempt = currentAttempt;
                    var current = UIErrors.CurrentContext;
                    // 合法移交或缓存接管后，以实际归还所有者为准；无宿主上下文时使用登记位置。
                    // 宿主级清理的 ViewId=0 是明确身份，不能混入原页面的句柄或路由。
                    var owner = current.HostId == Guid.Empty ? context : current;
                    context = new UIErrorContext(owner.HostId, owner.ViewId, owner.RouteKey,
                        current.Operation ?? "Cleanup", current.Phase ?? "Release");
                }

                state = CleanupResponsibilityState.Pending;
                if (attempts < long.MaxValue)
                {
                    ++attempts;
                }

                callback = release;
                CleanupRegistry.Retain(this);
            }

            _ = RunAttemptAsync(callback, completion);
            return completion.Task;
        }

        private async Task RunAttemptAsync(Func<ValueTask> callback, TaskCompletionSource<bool> completion)
        {
            var previous = currentRelease.Value;
            var frame = new ReleaseFrame { Owner = this, Parent = previous };
            currentRelease.Value = frame;
            try
            {
                using (UIErrors.BeginContext(context))
                {
                    await callback();
                }

                lock (gate)
                {
                    state = CleanupResponsibilityState.Completed;
                    failure = null;
                    release = null;
                    CleanupRegistry.ConfirmCompleted(this);
                    completion.TrySetResult(true);
                }
            }
            catch (Exception error)
            {
                UIErrors.AttachContext(error, context);
                lock (gate)
                {
                    state = CleanupResponsibilityState.Failed;
                    failure = error;
                    completion.TrySetException(error);
                    _ = completion.Task.Exception;
                }
            }
            finally
            {
                Volatile.Write(ref frame.Active, false);
                frame.Owner = null;
                currentRelease.Value = previous;
            }
        }

        private sealed class ReleaseFrame
        {
            internal CleanupResponsibility Owner;
            internal ReleaseFrame Parent;
            internal bool Active = true;
        }
    }
}
