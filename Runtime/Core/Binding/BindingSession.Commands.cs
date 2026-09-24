using System;
using System.Threading;

namespace MUI
{
    internal sealed partial class BindingSession
    {
        private int executingCommandCount;
        private static readonly AsyncLocal<CommandExecution> CurrentCommand = new AsyncLocal<CommandExecution>();

        /// <summary>跨异步调用链的在途命令数；用于诊断，不代替当前调用链的自身等待检查。</summary>
        internal int ExecutingCommandCount => Volatile.Read(ref executingCommandCount);

        internal bool IsExecutingCommand
        {
            get
            {
                for (var execution = CurrentCommand.Value; execution != null; execution = execution.Parent)
                {
                    if (execution.Active && ReferenceEquals(execution.Session, this))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal IDisposable EnterCommandExecution()
        {
            var execution = new CommandExecution(this, CurrentCommand.Value);
            CurrentCommand.Value = execution;
            return execution;
        }

        /// <summary>用会话身份识别自身等待，不能用共享 VM、共享命令或导航目标替代会话身份。</summary>
        private sealed class CommandExecution : IDisposable
        {
            internal BindingSession Session;
            internal readonly CommandExecution Parent;
            internal bool Active = true;

            internal CommandExecution(BindingSession session, CommandExecution parent)
            {
                Session = session;
                Parent = parent;
                Interlocked.Increment(ref session.executingCommandCount);
            }

            public void Dispose()
            {
                if (!Active)
                {
                    return;
                }

                Active = false;
                Interlocked.Decrement(ref Session.executingCommandCount);
                // 逃逸异步上下文可能继续持有标记，但不应因此保留已经结束的绑定会话。
                Session = null;
                CurrentCommand.Value = Parent;
            }
        }
    }
}
