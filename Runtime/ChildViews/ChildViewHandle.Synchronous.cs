using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs> where TViewModel : ViewModel
    {
        internal override bool CanCloseSynchronously => closeStarted
            ? closeCompleted
            : Owner.Mode == LifetimeMode.Synchronous && !IsLifecycleExecuting && !committing
                && !synchronousTransition && !IsUpdatingArgs && !IsRebinding
                && activation.CanDisposeSynchronously && instance.CanDisposeSynchronously
                && (lease == null || synchronousLease != null)
                && (binding == null || binding.CanUnbindSynchronously)
                && (lifecycle == null || lifecycle.CanReleaseModelSynchronously);

        private Task GetSynchronousCleanupCompletion()
        {
            Owner.RequireThread();
            if (close != null)
            {
                return close.Task;
            }

            var complete = closeCompleted;
            if (!closeStarted || (complete && closeFailure == null))
            {
                return Task.CompletedTask;
            }

            close = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (complete)
            {
                close.TrySetException(closeFailure);
                _ = close.Task.Exception;
            }

            return close.Task;
        }

        internal void AdoptSynchronous(ISynchronousViewLease acquired)
        {
            // 先接管凭证；View 属性或原生校验失败后仍能通过统一回滚释放。
            synchronousLease = acquired ?? throw new InvalidOperationException("Child provider returned no synchronous view lease.");
            view = acquired.View;
            if (view == null || !view.IsAlive)
            {
                throw new InvalidOperationException("Child provider returned an invalid View.");
            }

            if (view is IChildTickHost tickHost)
            {
                tickHost.ChildTickActivityChanged += Owner.RefreshTicks;
            }
        }

        /// <summary>同步关闭并完成资源释放；本实例的回调或命令内应使用 RequestClose。</summary>
        public override void Dispose()
        {
            Owner.RequireThread();
            if (Owner.Mode != LifetimeMode.Synchronous || IsExecuting || !CanCloseSynchronously)
            {
                throw new InvalidOperationException("Synchronous child disposal requires a synchronous idle lifecycle.");
            }

            StartClose();
            if (closeFailure != null)
            {
                ExceptionDispatchInfo.Capture(closeFailure).Throw();
            }
        }

        private void Release()
        {
            var errors = new List<Exception>(earlyErrors);
            EndActivation(errors);

            if (lifecycle != null)
            {
                lifecycle.Destroy(errors);
            }

            try
            {
                ViewInstanceCleanup.Run(instance, synchronousLease, errors, () =>
                {
                    if (view is IChildTickHost tickHost)
                    {
                        tickHost.ChildTickActivityChanged -= Owner.RefreshTicks;
                    }
                });
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            CompleteRelease(errors);
        }

        private void EndActivation(List<Exception> errors)
        {
            if (!activationCleanupComplete)
            {
                try
                {
                    if (lifecycle != null)
                    {
                        lifecycle.EndActivation(activation, committed, errors);
                    }
                    else
                    {
                        ViewActivationCleanup.Run(null, null, activation, errors);
                    }

                    bindingsCommitted = false;
                    committed = false;
                    activationCleanupComplete = true;
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
        }
    }
}
