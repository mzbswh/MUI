using System;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>同步借用新模型并换绑同一界面；无法恢复旧绑定时直接故障关闭。</summary>
        private RebindOutcome RebindUntraced<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TViewModel viewModel) where TViewModel : ViewModel
        {
            RequireSynchronousNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            if (IsShutdown)
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.HostClosed);
            }
            if (IsReentrant || HasCloseEvaluation || WouldWaitForSelf(source) ||
                (entries.TryGetValue(source, out var commandSource) && commandSource.IsExecutingBindingCommand))
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.Reentrant);
            }
            if (pending != 0 || rebinding.Contains(source) || !requests.Wait(0))
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.Busy);
            }

            rebinding.Add(source);
            BeginNavigationRequest();
            try
            {
                if (!entries.TryGetValue(source, out var entry) || !CanMutateInstance(entry))
                {
                    return new RebindOutcome(RebindStatus.Rejected, RebindRejection.SourceUnavailable);
                }
                if (!ReferenceEquals(entry.Route, route) || !(entry is ViewInstance<TViewModel, TArgs, TResult> instance))
                {
                    return new RebindOutcome(RebindStatus.Rejected, RebindRejection.RouteMismatch);
                }
                if (ownership.HasOwners(instance.Handle))
                {
                    return new RebindOutcome(RebindStatus.Rejected, RebindRejection.InUse);
                }

                if (instance.IsRebinding || instance.IsUpdatingArgs)
                {
                    return new RebindOutcome(RebindStatus.Rejected, RebindRejection.Busy);
                }

                var outcome = instance.Rebind(viewModel);
                PublishRebindOutcome(instance, outcome);
                return outcome;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
                return new RebindOutcome(RebindStatus.Failed, error: error);
            }
            finally
            {
                rebinding.Remove(source);
                requests.Release();
                EndNavigationRequest();
            }
        }
    }
}
