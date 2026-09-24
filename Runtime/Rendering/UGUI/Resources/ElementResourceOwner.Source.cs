using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.UGUI
{
    internal sealed partial class ElementResourceOwner<T> where T : class
    {
        private Lifetime sourceLifetime;
        private Action sourceChanged;
        private Action<Task<bool>> sourceOperationChanged;
        private string source;
        private string requestedSource;
        private bool requestingSource;
        private bool notifying;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>当前成功显示的资源键；在途请求和失败不会提前改写此值。</summary>
        internal string Source => source;

        internal void ConfigureSource(Lifetime owner, Action changed, Action<Task<bool>> operationChanged = null)
        {
            sourceLifetime = owner;
            sourceChanged = changed;
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

            sourceLifetime.Token.ThrowIfCancellationRequested();
            requestedSource = key;
            requestingSource = true;
            try
            {
                if (synchronous)
                {
                    sourceLifetime.Run(_ =>
                    {
                        if (key == null)
                        {
                            Slot.Clear();
                        }
                        else
                        {
                            Slot.Replace(key);
                        }
                    });
                }
                else
                {
                    // 所属生命周期跟踪完整加载和属性发布；观察器只负责诊断，不拥有资源。
                    var activation = sourceLifetime;
                    var operation = activation.RunAsync(async token =>
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
                    if (sourceOperationChanged != null)
                    {
                        var completion = operation.AsTask();
                        sourceOperationChanged(completion);
                        _ = ObserveSourceAsync(new ValueTask<bool>(completion), activation);
                    }
                    else
                    {
                        _ = ObserveSourceAsync(operation, activation);
                    }
                }
            }
            finally
            {
                requestingSource = false;
            }
        }

        /// <summary>原生 setter 也会调用外部监听，必须在整个调用期间阻止资源键重入。</summary>
        internal void AssignNative(Action assignment)
        {
            var previous = notifying;
            notifying = true;
            try
            {
                assignment();
            }
            catch (Exception error)
            {
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

        internal void CommitSource(bool clearing)
        {
            if (sourceLifetime == null)
            {
                return;
            }

            // 在原生赋值点更新，不能等旧凭证释放结束后才发布；期间可能已有更新的请求。
            var key = clearing ? null : requestedSource;
            if (source == key)
            {
                return;
            }

            source = key;
            Notify(sourceChanged);
        }

        private static async Task ObserveSourceAsync(ValueTask<bool> operation, Lifetime activation)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException) when (activation.IsEnded)
            {
                // 生命周期结束造成的取消不作为加载错误重复报告。
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
