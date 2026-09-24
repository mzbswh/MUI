using System;
using System.Threading;

namespace MUI
{
    /// <summary>单次命令调用的取消、来源与有效性上下文；不能跨调用保存并复用。</summary>
    public sealed class CommandContext
    {
        private static readonly AsyncLocal<CommandContext> Ambient = new AsyncLocal<CommandContext>();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Func<bool> isCurrent;
        private readonly ICommandTarget target;

        internal CommandContext(CancellationToken token, Func<bool> isCurrent, ICommandTarget target, IUICommandState command)
        {
            Token = token;
            this.isCurrent = isCurrent;
            this.target = target;
            Command = command;
        }

        internal static CommandContext Current => Ambient.Value;

        internal ICommandTarget Source => target;

        internal IUICommandState Command
        {
            get;
        }

        internal bool IsRunning
        {
            get; private set;
        }

        internal CommandContext ExecutionParent
        {
            get; private set;
        }

        public CancellationToken Token
        {
            get;
        }

        internal bool HasRequestedClose
        {
            get; private set;
        }

        /// <summary>在创建线程查询本次调用是否仍有效，不将有效性查询当作跨线程同步手段。</summary>
        public bool IsCurrent
        {
            get
            {
                RequireThread();
                return !Token.IsCancellationRequested && isCurrent() && (target == null || target.IsActive);
            }
        }

        internal IDisposable EnterExecution()
        {
            var previous = Ambient.Value;
            ExecutionParent = previous;
            Ambient.Value = this;
            IsRunning = true;
            return new ExecutionScope(this, previous);
        }

        private void RequireThread()
        {
            // 先检查线程，不能在后台线程调用来源属性或进入 UI 写入回调。
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("命令上下文必须在创建时的 UI 线程使用；请先切回该线程。");
            }
        }

        /// <summary>线程不正确时报告调用错误；调用已失效时抛出取消异常。</summary>
        public void ThrowIfInvalid()
        {
            if (!IsCurrent)
            {
                throw new OperationCanceledException("Command activation is no longer current.", Token);
            }
        }

        /// <summary>在 await 后用于 UI 或 ViewModel 写入，必须在 UI 线程调用。</summary>
        public void Apply(Action update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            ThrowIfInvalid();
            update();
        }

        public void RequestClose()
        {
            ThrowIfInvalid();
            if (target == null)
            {
                throw new InvalidOperationException("This command has no source childView.");
            }

            target.RequestClose();
            HasRequestedClose = true;
        }

        public void Complete<TResult>(TResult result)
        {
            ThrowIfInvalid();
            if (target == null)
            {
                throw new InvalidOperationException("This command has no source childView.");
            }

            target.Complete(result);
            HasRequestedClose = true;
        }

        private sealed class ExecutionScope : IDisposable
        {
            private readonly CommandContext owner;
            private readonly CommandContext previous;

            public ExecutionScope(CommandContext owner, CommandContext previous)
            {
                this.owner = owner;
                this.previous = previous;
            }

            public void Dispose()
            {
                owner.IsRunning = false;
                Ambient.Value = previous;
            }
        }
    }
}
