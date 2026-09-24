using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.UGUI
{
    public sealed partial class NestedViewElement
    {
        private bool synchronousCleanupFailed;
        private Exception synchronousPreparationFailure;
        private bool synchronousChange;
        private TaskCompletionSource<bool> synchronousCompletion;

        private Task GetPendingChange()
        {
            if (!synchronousChange)
            {
                return pendingChange ?? Task.CompletedTask;
            }

            if (synchronousCompletion != null)
            {
                return synchronousCompletion.Task;
            }

            if (!preparing && synchronousPreparationFailure == null)
            {
                return Task.CompletedTask;
            }

            // 仅显式查询兼容接口时创建信号，同步替换不依赖其完成状态。
            synchronousCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (synchronousPreparationFailure != null)
            {
                synchronousCompletion.TrySetException(synchronousPreparationFailure);
                _ = synchronousCompletion.Task.Exception;
            }

            return synchronousCompletion.Task;
        }

        bool IChildViewElement.TryCompleteSynchronousPreparation()
        {
            if (preparing)
            {
                return false;
            }

            if (synchronousPreparationFailure != null)
            {
                ExceptionDispatchInfo.Capture(synchronousPreparationFailure).Throw();
            }

            return true;
        }

        private void RequestSynchronousChange(ViewModel model, ChildViewScope targetScope,
            Lifetime targetLifetime, long request)
        {
            if (preparing)
            {
                throw new InvalidOperationException("A previous nested change has not finished.");
            }

            synchronousChange = true;
            synchronousCompletion = null;
            synchronousPreparationFailure = null;
            preparing = true;
            try
            {
                targetLifetime.Run(token => ChangeSynchronous(model, targetScope, request, token));
            }
            catch (Exception error)
            {
                synchronousPreparationFailure = error;
                throw;
            }
            finally
            {
                preparing = false;
                if (synchronousCompletion != null)
                {
                    if (synchronousPreparationFailure == null)
                    {
                        synchronousCompletion.TrySetResult(true);
                    }
                    else
                    {
                        synchronousCompletion.TrySetException(synchronousPreparationFailure);
                        _ = synchronousCompletion.Task.Exception;
                    }
                }
            }
        }

        private void ChangeSynchronous(ViewModel model, ChildViewScope targetScope, long request, CancellationToken token)
        {
            RequireSynchronousChangeCurrent(targetScope, request, token);
            var previous = childHandle;
            var previousModel = displayedModel;
            if (previous != null)
            {
                try
                {
                    previous.Dispose();
                }
                catch
                {
                    synchronousCleanupFailed = true;
                    throw;
                }
                finally
                {
                    if (previous.State == ChildViewState.Closed)
                    {
                        childHandle = null;
                        displayedModel = null;
                    }
                }
            }

            RequireSynchronousChangeCurrent(targetScope, request, token);
            if (model == null)
            {
                return;
            }

            Exception failure;
            try
            {
                childHandle = PrepareSynchronousModel(model, targetScope, request, token);
                displayedModel = model;
                return;
            }
            catch (Exception error)
            {
                failure = error;
            }

            if (!synchronousCleanupFailed && previousModel != null && !ReferenceEquals(previousModel, model)
                && IsSynchronousChangeCurrent(targetScope, request, token))
            {
                try
                {
                    childHandle = PrepareSynchronousModel(previousModel, targetScope, request, token);
                    displayedModel = previousModel;
                }
                catch (Exception restoration)
                {
                    throw new AggregateException("Synchronous nested replacement and restoration failed.", failure, restoration);
                }
            }

            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private ChildViewHandle PrepareSynchronousModel(ViewModel model, ChildViewScope targetScope,
            long request, CancellationToken token)
        {
            ChildViewHandle candidate = null;
            try
            {
                candidate = targetScope.PrepareSynchronous(template, provider, Unit.Value, model);
                RequireSynchronousChangeCurrent(targetScope, request, token);
                candidate.Commit();
                RequireSynchronousChangeCurrent(targetScope, request, token);
                return candidate;
            }
            catch (Exception failure)
            {
                if (failure is ChildViewPreparationException)
                {
                    // 同步准备只包装清理失败；意外传入异步清理异常也不能认定为安全可复用。
                    synchronousCleanupFailed = true;
                }

                if (candidate != null)
                {
                    try
                    {
                        candidate.Dispose();
                    }
                    catch (Exception cleanup)
                    {
                        synchronousCleanupFailed = true;
                        throw ChildViewPreparationException.SynchronousCleanupFailed(failure, cleanup);
                    }
                }

                throw;
            }
        }

        private bool IsSynchronousChangeCurrent(ChildViewScope targetScope, long request, CancellationToken token)
        {
            return IsAlive && !token.IsCancellationRequested && request == version
                && ReferenceEquals(scope, targetScope) && targetScope.IsActive;
        }

        private void RequireSynchronousChangeCurrent(ChildViewScope targetScope, long request, CancellationToken token)
        {
            if (!IsSynchronousChangeCurrent(targetScope, request, token))
            {
                throw new OperationCanceledException("Nested child activation changed during synchronous replacement.", token);
            }
        }
    }
}
