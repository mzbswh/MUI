using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 共享 ViewModel 命令状态，每次调用有独立来源和取消信号。
    /// 在 UI 线程创建和执行，异步委托更新状态前必须使用 CommandContext.Apply。
    /// 状态通知回到创建时的 SynchronizationContext，但业务委托不会自动切换线程。
    /// </summary>
    public sealed class AsyncCommand : ObservableObject, IUICommand
    {
        private readonly object gate = new object();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly SynchronizationContext uiContext = SynchronizationContext.Current;
        private readonly Func<CommandContext, ValueTask> execute;
        private readonly Func<bool> canExecute;
        private readonly List<Execution> executions = new List<Execution>();
        private readonly SemaphoreSlim queue = new SemaphoreSlim(1, 1);
        private readonly int capacity;
        private long version;
        private Exception error;

        /// <summary>接入立即完成的业务委托，沿用取消、输入来源与并发规则，不切换线程或延迟一帧。</summary>
        public AsyncCommand(Action<CommandContext> execute,
                    Func<bool> canExecute = null,
                    CommandConcurrency concurrency = CommandConcurrency.RejectWhileRunning,
                    int capacity = 32)
            : this(Adapt(execute), canExecute, concurrency, capacity)
        {
        }

        public AsyncCommand(Func<CommandContext, ValueTask> execute,
                    Func<bool> canExecute = null,
                    CommandConcurrency concurrency = CommandConcurrency.RejectWhileRunning,
                    int capacity = 32)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            if (!Enum.IsDefined(typeof(CommandConcurrency), concurrency))
            {
                throw new ArgumentOutOfRangeException(nameof(concurrency));
            }

            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.canExecute = canExecute;
            this.capacity = capacity;
            Concurrency = concurrency;
        }

        public CommandConcurrency Concurrency
        {
            get;
        }

        public bool IsExecuting
        {
            get
            {
                lock (gate)
                {
                    return executions.Count > 0;
                }
            }
        }

        public Exception Error
        {
            get
            {
                lock (gate)
                {
                    return error;
                }
            }
        }

        public bool CanExecute
        {
            get
            {
                RequireThread();
                lock (gate)
                {
                    if (!HasCapacity())
                    {
                        return false;
                    }
                }

                return canExecute == null || canExecute();
            }
        }

        private static Func<CommandContext, ValueTask> Adapt(Action<CommandContext> execute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            return context =>
            {
                execute(context);
                return default;
            };
        }

        private bool HasCapacity() => executions.Count < capacity && (Concurrency != CommandConcurrency.RejectWhileRunning || executions.Count == 0);

        public ValueTask<CommandOutcome> ExecuteAsync(ICommandTarget source = null, CancellationToken cancellationToken = default)
        {
            RequireThread();
            if (cancellationToken.IsCancellationRequested || (source != null && !source.IsActive))
            {
                return new ValueTask<CommandOutcome>(CommandOutcome.Cancelled());
            }

            if (Concurrency == CommandConcurrency.Queue)
            {
                for (var context = CommandContext.Current; context != null; context = context.ExecutionParent)
                {
                    if (context.IsRunning && ReferenceEquals(context.Command, this))
                    {
                        return new ValueTask<CommandOutcome>(CommandOutcome.Reentrant());
                    }
                }
            }

            lock (gate)
            {
                if (!HasCapacity())
                {
                    return new ValueTask<CommandOutcome>(CommandOutcome.Rejected());
                }
            }

            try
            {
                if (canExecute != null && !canExecute())
                {
                    return new ValueTask<CommandOutcome>(CommandOutcome.Rejected());
                }

                // 资格判断属于业务代码，可能同步取消或关闭来源。
                if (cancellationToken.IsCancellationRequested || (source != null && !source.IsActive))
                {
                    return new ValueTask<CommandOutcome>(CommandOutcome.Cancelled());
                }
            }
            catch (Exception failure)
            {
                lock (gate)
                {
                    error = failure;
                }

                Notify(nameof(Error));
                return new ValueTask<CommandOutcome>(CommandOutcome.Failed(failure));
            }

            Execution entry;
            Execution[] superseded;
            lock (gate)
            {
                if (!HasCapacity())
                {
                    return new ValueTask<CommandOutcome>(CommandOutcome.Rejected());
                }

                superseded = Concurrency == CommandConcurrency.RestartLatest ? executions.ToArray() : Array.Empty<Execution>();
                entry = new Execution
                {
                    Version = ++version,
                    Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                };
                executions.Add(entry);
                error = null;
            }

            // 取消回调可能重入命令，因此先发布新版本。
            foreach (var previous in superseded)
            {
                Cancel(previous);
            }

            // 观察者可能重入并添加更多工作，必须先预留队列位置。
            var queueAdmission = Concurrency == CommandConcurrency.Queue ? queue.WaitAsync(entry.Cancellation.Token) : null;
            NotifyState();
            return new ValueTask<CommandOutcome>(ExecuteCoreAsync(entry, source, queueAdmission));
        }

        private async Task<CommandOutcome> ExecuteCoreAsync(Execution entry, ICommandTarget source, Task queueAdmission)
        {
            // 在排队等待前记录调用线程，不能把错误的异步续接线程当成合法 UI 线程。
            var context = new CommandContext(entry.Cancellation.Token, () => IsCurrent(entry), source, this);
            var acquired = false;
            try
            {
                if (queueAdmission != null)
                {
                    await queueAdmission;
                    acquired = true;
                }

                context.ThrowIfInvalid();
                // 排队项到达执行位置时重新求值业务资格。
                if (canExecute != null && !canExecute())
                {
                    return CommandOutcome.Rejected();
                }

                // 资格判断也可能启动新的 RestartLatest 调用或取消当前调用。
                context.ThrowIfInvalid();
                using (context.EnterExecution())
                {
                    await execute(context);
                }

                if (!context.HasRequestedClose)
                {
                    context.ThrowIfInvalid();
                }

                return CommandOutcome.Succeeded();
            }
            catch (OperationCanceledException)
            {
                return CommandOutcome.Cancelled();
            }
            catch (Exception failure)
            {
                lock (gate)
                {
                    if (entry.Version == version)
                    {
                        error = failure;
                    }
                }

                Notify(nameof(Error));
                return CommandOutcome.Failed(failure);
            }
            finally
            {
                lock (gate)
                {
                    executions.Remove(entry);
                }

                entry.Cancellation.Dispose();
                if (acquired)
                {
                    queue.Release();
                }

                NotifyState();
            }
        }

        private bool IsCurrent(Execution entry)
        {
            lock (gate)
            {
                return executions.Contains(entry) && (Concurrency != CommandConcurrency.RestartLatest || entry.Version == version);
            }
        }

        public void Cancel()
        {
            Execution[] snapshot;
            lock (gate)
            {
                snapshot = executions.ToArray();
            }

            foreach (var entry in snapshot)
            {
                Cancel(entry);
            }
        }

        private static void Cancel(Execution entry)
        {
            try
            {
                entry.Cancellation.Cancel(throwOnFirstException: false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception failure)
            {
                UIErrors.Report(failure);
            }
        }

        public void NotifyCanExecuteChanged() => Notify(nameof(CanExecute));

        private void NotifyState()
        {
            Notify(nameof(IsExecuting));
            Notify(nameof(CanExecute));
            Notify(nameof(Error));
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("命令必须在创建时的 UI 线程求值和执行。");
            }
        }

        private void Notify(string property)
        {
            if (Thread.CurrentThread.ManagedThreadId == thread)
            {
                OnPropertyChangedSafely(property, UIErrors.Report);
                return;
            }

            // 异步完成或外部通知可来自后台线程，不能直接进入绑定观察者。
            if (uiContext == null)
            {
                UIErrors.Report(new InvalidOperationException("命令缺少 UI 同步上下文，无法派发后台状态通知。"));
                return;
            }

            try
            {
                uiContext.Post(_ =>
                {
                    if (Thread.CurrentThread.ManagedThreadId != thread)
                    {
                        UIErrors.Report(new InvalidOperationException("命令同步上下文未将通知派发到所属 UI 线程。"));
                        return;
                    }

                    // 通知仅携带属性名，观察者读取执行时的最新状态，不发布旧状态快照。
                    OnPropertyChangedSafely(property, UIErrors.Report);
                }, null);
            }
            catch (Exception error)
            {
                // 通知调度失败不应阻止命令释放队列许可及完成原调用结果。
                UIErrors.Report(error);
            }
        }

        private sealed class Execution
        {
            public long Version;
            public CancellationTokenSource Cancellation;
        }
    }
}
