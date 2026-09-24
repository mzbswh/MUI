using System;
using System.Threading;

namespace MUI
{
    /// <summary>
    /// 只在创建线程执行的同步命令，支持来源、取消、关闭请求与错误通知。
    /// 不提供排队或后台通知；执行、资格求值及状态通知期间的重入执行被拒绝。
    /// </summary>
    public sealed class SynchronousCommand : ObservableObject, ISynchronousUICommand
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Action<CommandContext> execute;
        private readonly Func<bool> canExecute;
        private CancellationTokenSource cancellation;
        private bool evaluating;
        private bool notifying;
        private Exception error;

        public SynchronousCommand(Action<CommandContext> execute, Func<bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public bool IsExecuting => cancellation != null;

        public Exception Error => error;

        public bool CanExecute
        {
            get
            {
                RequireThread();
                if (IsExecuting || evaluating)
                {
                    return false;
                }

                evaluating = true;
                try
                {
                    return canExecute == null || canExecute();
                }
                finally
                {
                    evaluating = false;
                }
            }
        }

        public CommandOutcome Execute(ICommandTarget source = null, CancellationToken cancellationToken = default)
        {
            RequireThread();
            if (IsExecuting || evaluating || notifying)
            {
                return CommandOutcome.Reentrant();
            }

            if (cancellationToken.IsCancellationRequested || (source != null && !source.IsActive))
            {
                return CommandOutcome.Cancelled();
            }

            try
            {
                if (!CanExecute)
                {
                    return CommandOutcome.Rejected();
                }
            }
            catch (Exception failure)
            {
                error = failure;
                NotifyState();
                return CommandOutcome.Failed(failure);
            }

            // 资格求值可关闭来源；开始执行前再校验，避免执行失效界面的命令。
            if (cancellationToken.IsCancellationRequested || (source != null && !source.IsActive))
            {
                return CommandOutcome.Cancelled();
            }

            using (var current = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cancellation = current;
                error = null;
                var context = new CommandContext(current.Token, () => ReferenceEquals(cancellation, current), source, this);
                try
                {
                    NotifyState();
                    context.ThrowIfInvalid();
                    using (context.EnterExecution())
                    {
                        execute(context);
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
                    error = failure;
                    return CommandOutcome.Failed(failure);
                }
                finally
                {
                    cancellation = null;
                    NotifyState();
                }
            }
        }

        public void Cancel()
        {
            RequireThread();
            if (cancellation == null)
            {
                return;
            }

            try
            {
                cancellation.Cancel(throwOnFirstException: false);
            }
            catch (Exception failure)
            {
                UIErrors.Report(failure);
            }
        }

        public void NotifyCanExecuteChanged()
        {
            RequireThread();
            NotifyState();
        }

        private void NotifyState()
        {
            if (notifying)
            {
                return;
            }

            notifying = true;
            try
            {
                OnPropertyChangedSafely(nameof(IsExecuting), UIErrors.Report);
                OnPropertyChangedSafely(nameof(CanExecute), UIErrors.Report);
                OnPropertyChangedSafely(nameof(Error), UIErrors.Report);
            }
            finally
            {
                notifying = false;
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("同步命令必须在创建时的 UI 线程使用。");
            }
        }
    }
}
