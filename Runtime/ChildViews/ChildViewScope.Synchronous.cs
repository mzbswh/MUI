using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewScope
    {
        /// <summary>在撤销父级资格前检查全部子项，不等待准备、回调或异步过渡。</summary>
        public bool CanDisposeSynchronously
        {
            get
            {
                RequireThread();
                if (Mode != LifetimeMode.Synchronous)
                {
                    return disposal != null && disposal.Task.IsCompleted;
                }

                if (synchronousDisposalStarted)
                {
                    return synchronousDisposalCompleted;
                }

                if (!operations.CanDisposeSynchronously || releasingRetainedViews)
                {
                    return false;
                }

                foreach (var handle in handles)
                {
                    if (handle.IsExecuting || handle.IsTransitioning || !handle.CanCloseSynchronously)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>显式读取兼容接口时才创建信号，同步释放不依赖该任务。</summary>
        private Task GetSynchronousCleanupCompletion()
        {
            RequireThread();
            if (synchronousCleanupCompletion != null)
            {
                return synchronousCleanupCompletion.Task;
            }

            if (!synchronousDisposalStarted || (synchronousDisposalCompleted && disposalFailure == null))
            {
                return Task.CompletedTask;
            }

            synchronousCleanupCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (synchronousDisposalCompleted)
            {
                synchronousCleanupCompletion.TrySetException(disposalFailure);
                _ = synchronousCleanupCompletion.Task.Exception;
            }

            return synchronousCleanupCompletion.Task;
        }

        /// <summary>
        /// 在纯同步父生命周期中准备子视图；工厂必须声明完整同步生命周期。
        /// 提供方、准备和失败回滚均使用同步入口，项目提供方无需实现异步方法。
        /// </summary>
        public ChildViewHandle<TViewModel, TArgs> PrepareSynchronous<TViewModel, TArgs>(
            ChildViewTemplate<TViewModel, TArgs> template, ISynchronousViewProvider provider,
            TArgs args, TViewModel assignedModel = null) where TViewModel : ViewModel
        {
            Validate(template, provider);
            if (Mode != LifetimeMode.Synchronous || !template.SupportsSynchronousLifecycle)
            {
                throw new InvalidOperationException("Synchronous child preparation requires a synchronous scope and lifecycle template.");
            }

            // 借用模型不由本 Scope 销毁，因此只约束工厂创建并转移所有权的模型。
            if (assignedModel == null && typeof(IAsyncDisposable).IsAssignableFrom(typeof(TViewModel))
                && !typeof(IDisposable).IsAssignableFrom(typeof(TViewModel)))
            {
                throw new InvalidOperationException("Synchronous child factories must return synchronously disposable owned models.");
            }

            if (provider.GetSyncAvailability(template.Resource) != SyncCreateAvailability.Available)
            {
                throw new InvalidOperationException("Child view resource cannot be created and released synchronously.");
            }

            return owner.Run(_ => operations.Run(__ =>
            {
                RequireActive();
                var handle = NewHandle(template, args);
                try
                {
                    handle.InitializeProvider(provider);
                    handle.CreateModel(assignedModel, true);
                    RequireActive();
                    handle.AdoptSynchronous(provider.Create(template.Resource));
                    RequireActive();
                    handle.Prepare();
                    handle.FinishPreparation();
                    return handle;
                }
                catch (Exception failure)
                {
                    var resourceFailure = failure as SynchronousResourceLoadException;
                    if (resourceFailure != null)
                    {
                        // 提供方未交出凭证，但已经报告回滚残留；句柄自身清理成功也不能抹去它。
                        cleanupErrors.Add(resourceFailure.CleanupError);
                    }

                    try
                    {
                        handle.Dispose();
                    }
                    catch (Exception cleanup)
                    {
                        throw ChildViewPreparationException.SynchronousCleanupFailed(failure, cleanup);
                    }

                    if (resourceFailure != null)
                    {
                        throw ChildViewPreparationException.SynchronousCleanupFailed(failure, resourceFailure.CleanupError);
                    }

                    throw;
                }
            }));
        }

        public void Dispose()
        {
            RequireThread();
            if (!CanDisposeSynchronously)
            {
                throw new InvalidOperationException("Child scope cannot be disposed synchronously while child operations or callbacks are active.");
            }

            if (synchronousDisposalStarted || Mode != LifetimeMode.Synchronous)
            {
                ThrowDisposalFailure();
                return;
            }

            synchronousDisposalStarted = true;
            try
            {
                try
                {
                    EndVisualRetention();
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }

                try
                {
                    Cancel();
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }

                try
                {
                    operations.Dispose();
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }

                foreach (var handle in handles.ToArray())
                {
                    try
                    {
                        handle.Dispose();
                    }
                    catch (Exception error)
                    {
                        // 已完成关闭的错误由 Remove 记录；准入失败仍保留在 Scope 中。
                        if (handles.Contains(handle))
                        {
                            cleanupErrors.Add(error);
                        }
                    }
                }
            }
            finally
            {
                parentCancellation.Dispose();
                disposalFailure = cleanupErrors.Count == 0 ? null : new AggregateException("Child scope cleanup failed.", cleanupErrors);
                synchronousDisposalCompleted = true;
                if (synchronousCleanupCompletion != null && disposalFailure == null)
                {
                    synchronousCleanupCompletion.TrySetResult(true);
                }
                else if (synchronousCleanupCompletion != null)
                {
                    synchronousCleanupCompletion.TrySetException(disposalFailure);
                    _ = synchronousCleanupCompletion.Task.Exception;
                }
            }

            ThrowDisposalFailure();
        }

        private void ThrowDisposalFailure()
        {
            if (disposalFailure != null)
            {
                ExceptionDispatchInfo.Capture(disposalFailure).Throw();
            }
        }

        /// <summary>将同步组合操作登记到 Scope 和父级，保护整个提交与旧资源释放过程。</summary>
        internal T RunSynchronous<T>(Func<T> operation)
        {
            RequireActive();
            if (Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("This operation requires a synchronous child scope.");
            }

            return owner.Run(_ => operations.Run(__ => operation()));
        }

        internal void RequireAsyncAllowed()
        {
            RequireThread();
            if (Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("This child scope does not accept asynchronous lifecycle operations.");
            }
        }
    }
}
