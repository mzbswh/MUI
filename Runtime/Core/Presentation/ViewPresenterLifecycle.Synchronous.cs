using System;
using System.Collections.Generic;

namespace MUI
{
    internal sealed partial class ViewPresenterLifecycle<TViewModel, TArgs, TResult>
        where TViewModel : ViewModel
    {
        /// <summary>宿主在决定同步销毁前检查自有模型；借用模型不构成同步释放限制。</summary>
        internal bool CanReleaseModelSynchronously => modelOwnership.CanDisposeSynchronously;

        /// <summary>
        /// 同步结束本次激活，保留实例资源供缓存或最终销毁。
        /// 不等待换绑，不运行异步关闭钩子，能力不足时保留原激活状态。
        /// </summary>
        internal Exception EndActivation(Lifetime activation, bool committed, List<Exception> errors)
        {
            if (IsRebinding)
            {
                throw new InvalidOperationException("Cannot end activation synchronously while rebinding is in progress.");
            }

            if (committed && opened && presenter is IAsyncClosePresenter)
            {
                throw new InvalidOperationException("Presenter requires asynchronous close.");
            }

            ViewActivationCleanup.RequireSynchronous(Binding, activation);
            if (completedRebind.HasValue)
            {
                var rebound = completedRebind.Value;
                if ((rebound.RecoveryFailed || rebound.Cleanup == RebindCleanup.Failed)
                    && rebound.Error != null && !errors.Contains(rebound.Error))
                {
                    errors.Add(rebound.Error);
                }
            }

            if (rebindCleanupFailure != null && !errors.Contains(rebindCleanupFailure))
            {
                errors.Add(rebindCleanupFailure);
            }

            Action close = null;
            if (opened)
            {
                close = () =>
                {
                    opened = false;
                    invoke(presenter.Close);
                };
            }

            var failure = ViewActivationCleanup.Run(close, Binding, activation, errors);
            Binding = null;
            return failure;
        }
    }
}
