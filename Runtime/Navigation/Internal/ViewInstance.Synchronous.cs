using System;
using System.Collections.Generic;
using MUI.Resources;

namespace MUI.Navigation
{
    internal sealed partial class ViewInstance<TViewModel, TArgs, TResult> where TViewModel : ViewModel
    {
        private int synchronousCallbackDepth;
        private bool synchronousReleasing;
        private CloseOutcome? synchronousReleaseOutcome;

        internal override LifetimeMode Mode => activationLifetime.Mode;

        internal override bool CanReleaseSynchronously => Mode == LifetimeMode.Synchronous &&
            !IsPreparing && !synchronousReleasing && synchronousCallbackDepth == 0 && !IsRebinding &&
            !IsUpdatingArgs && !IsExecutingBindingCommand &&
            activationLifetime.CanDisposeSynchronously &&
            (binding == null || binding.CanUnbindSynchronously) &&
            (content == null || content.CanReleaseSynchronously);

        internal override void AdoptSynchronousLease(ISynchronousViewLease acquired)
        {
            content.AdoptSynchronous(acquired);
            AttachHost();
        }

        /// <summary>直接结束激活并归还内容；同步缓存接管由调用方显式提供，不进入异步缓存队列。</summary>
        internal override CloseOutcome Release(DismissReason reason, bool wasCommitted,
            IReadOnlyList<Exception> initialErrors, Func<ViewContent, bool> retainContent = null,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease = null)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != threadId)
            {
                throw new InvalidOperationException("Synchronous view release requires its owning UI thread.");
            }

            if (synchronousReleaseOutcome.HasValue)
            {
                return synchronousReleaseOutcome.Value;
            }

            if (!CanReleaseSynchronously)
            {
                throw new InvalidOperationException("View instance cannot release synchronously during work or unsupported cleanup.");
            }

            var errors = new List<Exception>(initialErrors);
            var lifecycleFailure = failure;
            synchronousReleasing = true;
            try
            {
                try
                {
                    CancelActivation();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }

                if (argsUpdateCleanupFailure != null && !errors.Contains(argsUpdateCleanupFailure))
                {
                    errors.Add(argsUpdateCleanupFailure);
                }

                if (lifecycle != null)
                {
                    var closeFailure = lifecycle.EndActivation(activationLifetime, wasCommitted, errors);
                    lifecycleFailure = lifecycleFailure ?? closeFailure;
                }
                else
                {
                    ViewActivationCleanup.Run(null, null, activationLifetime, errors);
                }

                Owner.ReleaseDependencies(this, errors);

                if (content != null)
                {
                    DetachHost(errors);
                    var retained = false;
                    if (wasCommitted && !CleanupTimedOut && errors.Count == 0 &&
                        lifecycleFailure == null && lifecycle != null && lifecycle.OwnsModel &&
                        route.Policy.CacheMode != ViewCacheMode.None && presenter is IReusableViewPresenter &&
                        (reason == DismissReason.Closed || reason == DismissReason.Back || reason == DismissReason.Replaced))
                    {
                        try
                        {
                            retained = retainContent == null ? Owner.RetainContent(route, content) : retainContent(content);
                        }
                        catch (Exception error)
                        {
                            errors.Add(error);
                        }
                    }

                    if (!retained)
                    {
                        content.Release(errors, beginResourceRelease);
                    }

                    content = null;
                }

                var outcome = CompleteRelease(reason, lifecycleFailure, errors);
                synchronousReleaseOutcome = outcome;
                return outcome;
            }
            finally
            {
                synchronousReleasing = false;
            }
        }
    }
}
