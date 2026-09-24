using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        public bool TryCompleteChildPreparation()
        {
            var scope = childViews;
            var resourceContext = activeResourceContext;
            RequirePreparationActivation(scope, resourceContext);
            if (resourceContext != null && !resourceContext.TryCompletePreparation())
            {
                return false;
            }
            foreach (var element in elements)
            {
                if (element == null || !(element is IChildViewElement child))
                {
                    continue;
                }

                if (scope.Mode == LifetimeMode.Synchronous)
                {
                    var completed = child.TryCompleteSynchronousPreparation();
                    RequirePreparationActivation(scope, resourceContext);
                    if (!completed)
                    {
                        return false;
                    }

                    continue;
                }

                var preparation = child.Preparation;
                RequirePreparationActivation(scope, resourceContext);
                if (!preparation.IsCompleted)
                {
                    return false;
                }

                preparation.GetAwaiter().GetResult();
            }

            return true;
        }

        private void RequirePreparationActivation(ChildViewScope scope, ViewResourceContext resourceContext)
        {
            RequireAlive();
            if (scope == null || !scope.IsActive || !ReferenceEquals(childViews, scope) ||
                !ReferenceEquals(activeResourceContext, resourceContext))
            {
                throw new InvalidOperationException("View 准备所属激活已结束或被替换。");
            }
        }

        public void CommitChildActivation()
        {
            var scope = childViews;
            var resourceContext = activeResourceContext;
            RequirePreparationActivation(scope, resourceContext);
            if (!TryCompleteChildPreparation())
            {
                throw new InvalidOperationException("Required child preparation changed during parent commit.");
            }

            RequirePreparationActivation(scope, resourceContext);
            scope.CommitActivation();
            RequirePreparationActivation(scope, resourceContext);
            if (resourceContext != null)
            {
                resourceContext.CommitPreparation();
            }
        }

        public async ValueTask CompleteChildPreparationAsync(CancellationToken cancellationToken)
        {
            var activationToken = childActivationToken;
            var scope = childViews;
            var resourceContext = activeResourceContext;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                activationToken.ThrowIfCancellationRequested();
                RequirePreparationActivation(scope, resourceContext);
                if (TryCompleteChildPreparation())
                {
                    RequirePreparationActivation(scope, resourceContext);
                    return;
                }

                if (resourceContext != null)
                {
                    await resourceContext.CompletePreparationAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    activationToken.ThrowIfCancellationRequested();
                    RequirePreparationActivation(scope, resourceContext);
                }

                foreach (var element in elements)
                {
                    if (element == null || !(element is IChildViewElement child))
                    {
                        continue;
                    }

                    var preparation = child.Preparation;
                    RequirePreparationActivation(scope, resourceContext);
                    try
                    {
                        await WaitForPreparationAsync(preparation, cancellationToken, activationToken);
                    }
                    catch (Exception) when (IsAlive && ReferenceEquals(childViews, scope) && element != null &&
                        !ReferenceEquals(preparation, child.Preparation))
                    {
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    activationToken.ThrowIfCancellationRequested();
                    RequirePreparationActivation(scope, resourceContext);
                }
            }
        }

        /// <summary>取消只结束本次等待，原准备任务仍由子控件及所属生命周期观察和清理。</summary>
        private static async ValueTask WaitForPreparationAsync(Task preparation, CancellationToken callerToken,
            CancellationToken activationToken)
        {
            callerToken.ThrowIfCancellationRequested();
            activationToken.ThrowIfCancellationRequested();
            if (!preparation.IsCompleted && (callerToken.CanBeCanceled || activationToken.CanBeCanceled))
            {
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (callerToken.Register(() => cancelled.TrySetResult(true)))
                using (activationToken.Register(() => cancelled.TrySetResult(true)))
                {
                    await Task.WhenAny(preparation, cancelled.Task);
                    callerToken.ThrowIfCancellationRequested();
                    activationToken.ThrowIfCancellationRequested();
                }
            }

            await preparation;
        }
    }
}
