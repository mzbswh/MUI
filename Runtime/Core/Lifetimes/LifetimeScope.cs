using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 为一次激活或实例持有资源并跟踪同步操作与合作式异步操作。
    /// Cancel 立即使生命周期失效；DisposeAsync 等待操作退出，
    /// 并按注册逆序释放资源。不能在本生命周期跟踪的操作内
    /// 等待它的销毁，否则会形成自等待。
    /// </summary>
    public sealed partial class LifetimeScope : IAsyncDisposable
    {
        private static readonly AsyncLocal<ReleaseFrame> CurrentRelease = new AsyncLocal<ReleaseFrame>();
        private static readonly AsyncLocal<OperationFrame> CurrentOperation = new AsyncLocal<OperationFrame>();
        private readonly object gate = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly CancellationToken token;
        private readonly List<ReleaseRegistration> releases = new List<ReleaseRegistration>();
        private readonly HashSet<object> owned = new HashSet<object>(ReferenceComparer.Instance);
        private readonly HashSet<Task> operations = new HashSet<Task>();
        private readonly TaskCompletionSource<bool> cancellationFinished;
        private readonly List<Exception> cancellationErrors = new List<Exception>();
        private TaskCompletionSource<bool> disposal;
        private bool disposalStarted;
        private bool ended;
        private bool disposed;
        private Exception disposalFailure;

        public LifetimeScope()
        {
            cancellationFinished = NewCompletion();
            token = cancellation.Token;
        }

        /// <summary>释放后仍可读取令牌。</summary>
        public CancellationToken Token => token;

        /// <summary>已登记且尚未结束的操作数；不包含已经完成、等待资源清理的操作。</summary>
        public int PendingOperationCount
        {
            get
            {
                lock (gate)
                {
                    return operations.Count;
                }
            }
        }

        /// <summary>Cancel 或 DisposeAsync 开始后立即为 true。</summary>
        public bool IsEnded
        {
            get
            {
                lock (gate)
                {
                    return ended || disposalStarted;
                }
            }
        }

        /// <summary>所有清理尝试完成后为 true，包含清理失败的情况。</summary>
        public bool IsDisposed
        {
            get
            {
                lock (gate)
                {
                    return disposed;
                }
            }
        }

        /// <summary>
        /// 仅注册成功时转移所有权；注册失败后资源仍归调用者。
        /// 同一个对象不能在本生命周期内重复注册。
        /// </summary>
        public T Own<T>(T resource)
            where T : class, IAsyncDisposable
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (ReferenceEquals(resource, this))
            {
                throw new ArgumentException("A lifetime cannot own itself.", nameof(resource));
            }

            Register(resource, resource.DisposeAsync);
            return resource;
        }

        /// <summary>托管同步资源；带能力查询的资源须在登记时可同步释放，否则所有权仍归调用者。</summary>
        public T OwnDisposable<T>(T resource)
            where T : class, IDisposable
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (resource is ISynchronousDisposable guarded && !guarded.CanDisposeSynchronously)
            {
                throw new InvalidOperationException("Resource is not currently eligible for synchronous ownership.");
            }
            Register(resource, () =>
            {
                resource.Dispose();
                return default;
            });
            return resource;
        }

        /// <summary>登记同步清理，回调按注册逆序执行。</summary>
        public void OnDispose(Action cleanup)
        {
            if (cleanup == null)
            {
                throw new ArgumentNullException(nameof(cleanup));
            }

            Register(new object(), () =>
            {
                cleanup();
                return default;
            });
        }

        /// <summary>登记可等待清理，与托管资源按同一逆序执行。</summary>
        public void OnDisposeAsync(Func<ValueTask> cleanup)
        {
            if (cleanup == null)
            {
                throw new ArgumentNullException(nameof(cleanup));
            }

            Register(new object(), cleanup);
        }

        private void Register(object identity, Func<ValueTask> release)
        {
            lock (gate)
            {
                ThrowIfEnded();
                if (!owned.Add(identity))
                {
                    throw new InvalidOperationException("Resource is already owned by this lifetime.");
                }

                releases.Add(new ReleaseRegistration(release));
            }
        }

        /// <summary>
        /// 调用操作前先登记；调用者必须等待结果并处理失败。
        /// 销毁只观察完成，不将业务错误或取消转换为清理失败。
        /// 操作写入 UI 前必须检查 Token。
        /// </summary>
        public ValueTask<T> RunAsync<T>(Func<CancellationToken, ValueTask<T>> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                ThrowIfEnded();
                operations.Add(completion.Task);
            }

            // ExecuteAsync 捕获所有操作失败，并完成对外任务。
            _ = ExecuteAsync(operation, completion);
            return new ValueTask<T>(completion.Task);
        }

        /// <summary>
        /// 启动由框架独立持有的清理任务，不继承发起者的操作调用链。
        /// 在途操作仍登记在原生命周期中，销毁仍须等待它们；仅隔离自等待判定的上下文。
        /// 调用方不得把返回任务交回发起操作等待，且仍须观察最终清理结果。
        /// </summary>
        internal static Task StartIndependentCleanup(Func<Task> cleanup)
        {
            var previous = CurrentOperation.Value;
            CurrentOperation.Value = null;
            try
            {
                return cleanup();
            }
            finally
            {
                CurrentOperation.Value = previous;
            }
        }

        private async Task ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> operation, TaskCompletionSource<T> completion)
        {
            var previous = CurrentOperation.Value;
            var frame = new OperationFrame { Owner = this, Parent = previous };
            CurrentOperation.Value = frame;
            try
            {
                token.ThrowIfCancellationRequested();
                completion.TrySetResult(await operation(token));
            }
            catch (OperationCanceledException error)
            {
                completion.TrySetCanceled(error.CancellationToken);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
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
            }
        }

        /// <summary>
        /// 停止接受资源所有权和新操作，并且只请求一次取消。
        /// 资源持续持有直到 DisposeAsync。取消回调错误被保留，
        /// 由释放入口报告，不会跳过后续清理。
        /// </summary>
        public void Cancel()
        {
            lock (gate)
            {
                if (ended)
                {
                    return;
                }

                ended = true;
            }

            CompleteCancellation();
        }

        private void CompleteCancellation()
        {
            try
            {
                cancellation.Cancel(throwOnFirstException: false);
            }
            catch (Exception error)
            {
                lock (gate)
                {
                    cancellationErrors.Add(error);
                }
            }
            finally
            {
                lock (gate)
                {
                }

                if (cancellationFinished != null)
                {
                    cancellationFinished.TrySetResult(true);
                }
            }
        }

        /// <summary>
        /// 所有调用者观察相同的清理结果；此处不隐藏超时机制。
        /// 不响应取消的操作会使销毁保持等待，资源继续隔离保留。
        /// 资源要求 Unity 线程归属时，应在所属 UI 线程调用。
        /// 正在跟踪的异步操作尝试等待自身生命周期时，在修改取消/清理状态前拒绝。
        /// 清理回调尝试等待正在释放自身的生命周期时，得到失败的
        /// ValueTask，而不是加入自身等待；其他清理仍继续执行。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            // 尚未改变取消或清理状态；必须先拒绝操作等待其自身退出的清理流程。
            for (var operation = CurrentOperation.Value; operation != null; operation = operation.Parent)
            {
                if (Volatile.Read(ref operation.Active) && ReferenceEquals(operation.Owner, this))
                {
                    return new ValueTask(Task.FromException(new InvalidOperationException(
                        "异步操作不能等待正在跟踪它的生命周期销毁。")));
                }
            }

            for (var frame = CurrentRelease.Value; frame != null; frame = frame.Parent)
            {
                if (frame.Active && ReferenceEquals(frame.Owner, this))
                {
                    return new ValueTask(Task.FromException(new InvalidOperationException("A cleanup callback cannot await the lifetime that is releasing it.")));
                }
            }

            TaskCompletionSource<bool> completion;
            lock (gate)
            {
                if (disposal != null)
                {
                    return new ValueTask(disposal.Task);
                }

                completion = disposal = NewCompletion();
                disposalStarted = true;
            }

            Cancel();
            _ = DisposeCoreAsync(completion);
            return new ValueTask(completion.Task);
        }

        private async Task DisposeCoreAsync(TaskCompletionSource<bool> completion)
        {
            var errors = new List<Exception>();
            try
            {
                await cancellationFinished.Task;
                Task[] pending;
                ReleaseRegistration[] cleanup;
                lock (gate)
                {
                    pending = new Task[operations.Count];
                    operations.CopyTo(pending);
                    cleanup = releases.ToArray();
                    errors.AddRange(cancellationErrors);
                }

                foreach (var task in pending)
                {
                    // 业务操作错误归 RunAsync 调用者处理，不归销毁流程处理。
                    try
                    {
                        await task;
                    }
                    catch (Exception)
                    {
                    }
                }

                for (var i = cleanup.Length - 1; i >= 0; --i)
                {
                    var previous = CurrentRelease.Value;
                    var frame = new ReleaseFrame
                    {
                        Owner = this,
                        Parent = previous
                    };
                    CurrentRelease.Value = frame;
                    try
                    {
                        await cleanup[i].Asynchronous();
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                    finally
                    {
                        frame.Active = false;
                        frame.Owner = null;
                        CurrentRelease.Value = previous;
                    }
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            finally
            {
                CompleteDisposal(completion, errors);
            }
        }

        private void CompleteDisposal(TaskCompletionSource<bool> completion, List<Exception> errors)
        {
            cancellation.Dispose();
            lock (gate)
            {
                releases.Clear();
                owned.Clear();
                operations.Clear();
                cancellationErrors.Clear();
                AppendOperationCleanupFailures(errors);
                disposed = true;
                disposalFailure = errors.Count == 0 ? null : new AggregateException("LifetimeScope cleanup failed.", errors);
                if (completion == null)
                {
                    return;
                }

                if (disposalFailure == null)
                {
                    completion.TrySetResult(true);
                }
                else
                {
                    completion.TrySetException(disposalFailure);
                    // 同步调用者直接接收异常；标记任务异常已观察，异步调用者仍能得到相同结果。
                    _ = completion.Task.Exception;
                }
            }
        }

        private void ThrowIfEnded()
        {
            if (ended || disposalStarted)
            {
                throw new ObjectDisposedException(nameof(LifetimeScope));
            }
        }

        private static TaskCompletionSource<bool> NewCompletion()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed class OperationFrame
        {
            internal LifetimeScope Owner;
            internal OperationFrame Parent;
            internal bool Active = true;
        }

        private sealed class ReleaseFrame
        {
            public LifetimeScope Owner;
            public ReleaseFrame Parent;
            public bool Active = true;
        }

        private readonly struct ReleaseRegistration
        {
            internal ReleaseRegistration(Func<ValueTask> release) => Asynchronous = release;

            internal Func<ValueTask> Asynchronous { get; }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object left, object right) => ReferenceEquals(left, right);

            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
