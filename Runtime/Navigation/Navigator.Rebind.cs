using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly HashSet<ViewHandle> rebinding = new HashSet<ViewHandle>();

        /// <summary>包括排队中的换绑，避免旧命令在换绑之后入队而形成互相等待。</summary>
        private bool IsRebindingSourceCommand
        {
            get
            {
                for (var command = CommandContext.Current; command != null; command = command.ExecutionParent)
                {
                    if (command.IsRunning && command.Source is ViewInstance source && rebinding.Contains(source.Handle))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private ValueTask<RebindOutcome> RebindAsyncUntraced<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TViewModel viewModel, CancellationToken cancellationToken = default)
            where TViewModel : ViewModel
        {
            AssertThread();
            RequireAsyncNavigation();
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
                return RejectRebind(RebindRejection.HostClosed);
            }

            // 入队前检查会话命令，不能让它排在正等待同一命令结束的操作后面。
            if (IsReentrant || HasCloseEvaluation || WouldWaitForSelf(source) ||
                (entries.TryGetValue(source, out var sourceInstance) && sourceInstance.IsExecutingBindingCommand))
            {
                return RejectRebind(RebindRejection.Reentrant);
            }

            if (pending >= queueCapacity || !rebinding.Add(source))
            {
                return RejectRebind(RebindRejection.Busy);
            }

            BeginNavigationRequest();
            return new ValueTask<RebindOutcome>(RebindCoreAsync(source, route, viewModel, cancellationToken));
        }

        private async Task<RebindOutcome> RebindCoreAsync<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TViewModel viewModel, CancellationToken token)
            where TViewModel : ViewModel
        {
            var acquired = false;
            try
            {
                using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, shutdown.Token))
                {
                    await requests.WaitAsync(cancellation.Token);
                    acquired = true;
                    AssertThread();
                    cancellation.Token.ThrowIfCancellationRequested();
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

                    var outcome = await instance.RebindAsync(viewModel, cancellation.Token);
                    PublishRebindOutcome(instance, outcome);

                    return outcome;
                }
            }
            catch (OperationCanceledException)
            {
                return new RebindOutcome(RebindStatus.Cancelled);
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
                return new RebindOutcome(RebindStatus.Failed, error: error);
            }
            finally
            {
                if (acquired)
                {
                    requests.Release();
                }

                rebinding.Remove(source);
                EndNavigationRequest();
            }
        }

        internal void CloseFailedRebind(ViewInstance instance) => CloseFailedTransaction(instance, DismissReason.RebindFailed);

        private void PublishRebindOutcome(ViewInstance instance, RebindOutcome outcome)
        {
            if (outcome.Error != null)
            {
                using (EnterCallback(instance))
                {
                    UIErrors.Report(outcome.Error);
                }
            }

            QueueLifecycleEvent(instance, NavigationEventKind.ViewModelRebindFinished, rebind: outcome);
            DispatchLifecycleEvents();
        }

        private static ValueTask<RebindOutcome> RejectRebind(RebindRejection rejection) =>
            new ValueTask<RebindOutcome>(new RebindOutcome(RebindStatus.Rejected, rejection));
    }
}
