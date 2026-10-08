using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.UGUI
{
    internal sealed partial class ElementResourceOwner<T> where T : class
    {
        private LifetimeScope sourceLifetime;
        private Action sourceChanged;
        private Action sourceStateChanged;
        private Action<Task<bool>> sourceOperationChanged;
        private string source;
        private string requestedSource;
        private ResourceSourceState sourceState;
        private Exception sourceFailure;
        private long sourceGeneration;
        private bool requestingSource;
        private bool notifying;
        private bool assigningBorrowed;
        private ResourceSourceState borrowedState;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>当前成功显示的资源键；在途请求和失败不会提前改写此值。</summary>
        internal string Source => source;

        internal ResourceSourceSnapshot SourceSnapshot => new ResourceSourceSnapshot(
            requestedSource, source, sourceState, sourceFailure, Slot != null && Slot.IsFrozen,
            Slot == null ? null : Slot.FirstCleanupFailure, Slot == null ? 0 : Slot.CleanupFailureCount);

        internal void ConfigureSource(LifetimeScope owner, Action changed, Action stateChanged,
            Action<Task<bool>> operationChanged = null)
        {
            sourceLifetime = owner;
            sourceChanged = changed;
            sourceStateChanged = stateChanged;
            sourceOperationChanged = operationChanged;
        }

        internal void SetSource(string value)
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("资源键必须在所属 UI 线程更新。");
            }

            if (sourceLifetime == null)
            {
                throw new InvalidOperationException("请先配置资源键加载器和生命周期，不能混用直接资源槽与资源键绑定。");
            }

            if (requestingSource || notifying)
            {
                throw new InvalidOperationException("不能在资源赋值通知中重入资源键更新。");
            }

            var key = string.IsNullOrEmpty(value) ? null : value;
            if (key != null && string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("资源键不能只包含空白字符。", nameof(value));
            }

            var activation = sourceLifetime;
            activation.Token.ThrowIfCancellationRequested();
            requestedSource = key;
            var request = ++sourceGeneration;
            sourceState = ResourceSourceState.Loading;
            sourceFailure = null;
            requestingSource = true;
            try
            {
                Notify(sourceStateChanged);
                // 所属生命周期跟踪完整加载和属性发布；观察器只负责诊断，不拥有资源。
                ValueTask<bool> operation;
                try
                {
                    operation = activation.RunAsync(async token =>
                    {
                        bool applied;
                        if (key == null)
                        {
                            await Slot.ClearAsync();
                            applied = true;
                        }
                        else
                        {
                            applied = await Slot.ReplaceAsync(key, token);
                        }

                        return applied;
                    });
                }
                catch (Exception error)
                {
                    if (request == sourceGeneration)
                    {
                        sourceState = activation.IsEnded || error is OperationCanceledException
                            ? ResourceSourceState.Cancelled : ResourceSourceState.Failed;
                        sourceFailure = sourceState == ResourceSourceState.Failed ? error : null;
                        Notify(sourceStateChanged);
                    }

                    throw;
                }
                var completion = operation.AsTask();
                _ = ObserveSourceAsync(completion, request);
                sourceOperationChanged?.Invoke(completion);
            }
            finally
            {
                requestingSource = false;
            }
        }

        /// <summary>直接对象赋值复用同一槽及清理代际；原生引用立即替换，异步归还仍由槽托管。</summary>
        internal void SetBorrowed(T value)
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("资源对象必须在所属 UI 线程更新。");
            }

            if (requestingSource || notifying)
            {
                throw new InvalidOperationException("不能在资源赋值通知中重入资源对象更新。");
            }

            sourceLifetime.Token.ThrowIfCancellationRequested();
            Slot.RequireMutable();
            requestedSource = null;
            var request = ++sourceGeneration;
            sourceFailure = null;
            requestingSource = true;
            assigningBorrowed = true;
            borrowedState = value == null ? ResourceSourceState.Empty : ResourceSourceState.Displayed;
            try
            {
                var operation = Slot.SetBorrowedAsync(value).AsTask();
                _ = ObserveSourceAsync(operation, request,
                    borrowedState);
                sourceOperationChanged?.Invoke(operation);
                // setter 不等待异步归还，但原生同步赋值失败必须立即反馈给调用方。
                if (operation.IsCompleted)
                {
                    operation.GetAwaiter().GetResult();
                }
            }
            finally
            {
                assigningBorrowed = false;
                requestingSource = false;
            }
        }

        /// <summary>原生 setter 也会调用外部监听，必须在整个调用期间阻止资源键重入。</summary>
        internal void AssignNative(Action assignment, Func<bool> completedAfterFailure = null, Action suspendDisplay = null)
        {
            var previous = notifying;
            notifying = true;
            try
            {
                assignment();
            }
            catch (Exception error)
            {
                if (completedAfterFailure != null)
                {
                    try
                    {
                        if (completedAfterFailure())
                        {
                            // 适配器确认原生引用和渲染器均已解除，保留错误诊断。
                            UIErrors.Report(error);
                            return;
                        }
                    }
                    catch (Exception verificationError)
                    {
                        UIErrors.Report(verificationError);
                    }
                }

                try
                {
                    suspendDisplay?.Invoke();
                }
                catch (Exception suspensionError)
                {
                    UIErrors.Report(suspensionError);
                }

                // 不猜测异常发生在原生引用修改之前还是之后，交给槽保留可能仍被借用的资源。
                throw new ResourceAssignmentException(error);
            }
            finally
            {
                notifying = previous;
            }
        }

        /// <summary>原生赋值已提交后隔离通知异常，同时阻止通知内的资源键重入。</summary>
        internal void Notify(Action notification)
        {
            using (BeginResourcePhase("Notification"))
            {
                var previous = notifying;
                notifying = true;
                try
                {
                    notification();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
                finally
                {
                    notifying = previous;
                }
            }
        }

        internal void CommitSource(bool clearing)
        {
            if (sourceLifetime == null)
            {
                return;
            }

            // 在原生赋值点更新，不能等旧凭证释放结束后才发布；期间可能已有更新的请求。
            var key = clearing ? null : requestedSource;
            if (assigningBorrowed)
            {
                sourceState = borrowedState;
            }
            if (source == key)
            {
                if (assigningBorrowed)
                {
                    Notify(sourceStateChanged);
                }
                return;
            }

            source = key;
            Notify(sourceChanged);
            Notify(sourceStateChanged);
        }

        private async Task ObserveSourceAsync(Task<bool> operation, long request,
            ResourceSourceState? completedState = null)
        {
            try
            {
                var applied = await operation;
                if (request == sourceGeneration)
                {
                    sourceState = !applied ? ResourceSourceState.Cancelled :
                        completedState ?? (requestedSource == null ? ResourceSourceState.Empty : ResourceSourceState.Displayed);
                    Notify(sourceStateChanged);
                }
            }
            catch (OperationCanceledException)
            {
                // 加载或所属生命周期的可预期取消只更新状态，不产生错误报告。
                if (request == sourceGeneration)
                {
                    sourceState = ResourceSourceState.Cancelled;
                    Notify(sourceStateChanged);
                }
            }
            catch (Exception error)
            {
                if (request == sourceGeneration)
                {
                    sourceState = ResourceSourceState.Failed;
                    sourceFailure = error;
                    Notify(sourceStateChanged);
                }
                UIErrors.Report(error);
            }
        }
    }
}
