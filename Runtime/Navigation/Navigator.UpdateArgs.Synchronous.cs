using System;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>同步准备、提交及回滚参数；不等待队列，不重新打开界面。</summary>
        private ArgsUpdateOutcome UpdateArgsUntraced<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TArgs args) where TViewModel : ViewModel
        {
            RequireSynchronousNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (IsShutdown)
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.HostClosed);
            }

            if (IsReentrant || HasCloseEvaluation || WouldWaitForSelf(source) ||
                (entries.TryGetValue(source, out var commandSource) && commandSource.IsExecutingBindingCommand))
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Reentrant);
            }

            if (pending != 0 || !requests.Wait(0))
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Busy);
            }

            BeginNavigationRequest();
            try
            {
                if (!entries.TryGetValue(source, out var entry) || !CanMutateInstance(entry))
                {
                    return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.SourceUnavailable);
                }

                if (!ReferenceEquals(entry.Route, route) || !(entry is ViewInstance<TViewModel, TArgs, TResult> instance))
                {
                    return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.RouteMismatch);
                }

                if (ownership.HasOwners(instance.Handle))
                {
                    return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.InUse);
                }

                if (instance.IsUpdatingArgs || instance.IsRebinding)
                {
                    return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Busy);
                }

                if (instance.SynchronousArgsUpdater == null)
                {
                    return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Unsupported);
                }

                if (!(instance.View is IInputView))
                {
                    return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.InputControlUnsupported);
                }

                var dependencyRejection = CheckRetainedDependencies(instance, route, args, out var beforeCommit);
                if (dependencyRejection != ArgsUpdateRejection.None)
                {
                    return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, dependencyRejection);
                }

                var outcome = instance.UpdateArgs(args, beforeCommit);
                PublishArgsUpdateOutcome(instance, outcome);
                return outcome;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
                return new ArgsUpdateOutcome(ArgsUpdateStatus.PreparationFailed, error: error);
            }
            finally
            {
                requests.Release();
                EndNavigationRequest();
            }
        }
    }
}
