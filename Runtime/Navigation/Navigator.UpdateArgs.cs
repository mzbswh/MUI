using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly HashSet<ViewHandle> updatingArgs = new HashSet<ViewHandle>();

        private ValueTask<ArgsUpdateOutcome> UpdateArgsAsyncUntraced<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TArgs args, CancellationToken cancellationToken = default)
            where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (IsShutdown)
            {
                return RejectArgsUpdate(ArgsUpdateRejection.HostClosed);
            }

            if (IsReentrant || HasCloseEvaluation)
            {
                return RejectArgsUpdate(ArgsUpdateRejection.Reentrant);
            }

            if (pending >= queueCapacity || rebinding.Contains(source) || !updatingArgs.Add(source))
            {
                return RejectArgsUpdate(ArgsUpdateRejection.Busy);
            }

            BeginNavigationRequest();
            return new ValueTask<ArgsUpdateOutcome>(UpdateArgsCoreAsync(source, route, args, cancellationToken));
        }

        private async Task<ArgsUpdateOutcome> UpdateArgsCoreAsync<TViewModel, TArgs, TResult>(ViewHandle source,
            Route<TViewModel, TArgs, TResult> route, TArgs args, CancellationToken token)
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

                    if (instance.ArgsUpdater == null)
                    {
                        return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Unsupported);
                    }

                    if (!(instance.View is IInputView))
                    {
                        return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.InputControlUnsupported);
                    }

                    if (instance.IsRebinding || instance.IsUpdatingArgs)
                    {
                        return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, ArgsUpdateRejection.Busy);
                    }

                    var dependencyRejection = CheckRetainedDependencies(instance, route, args, out var beforeCommit);
                    if (dependencyRejection != ArgsUpdateRejection.None)
                    {
                        return new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, dependencyRejection);
                    }

                    acquired = false;
                    requests.Release();

                    async ValueTask AcquireCommitAsync(CancellationToken operationToken)
                    {
                        await requests.WaitAsync(operationToken);
                        acquired = true;
                        try
                        {
                            AssertThread();
                            operationToken.ThrowIfCancellationRequested();
                            if (!entries.TryGetValue(source, out var current) ||
                                !ReferenceEquals(current, instance) || !CanMutateInstance(instance) ||
                                instance.IsRebinding || ownership.HasOwners(instance.Handle))
                            {
                                throw new OperationCanceledException("页面在参数准备期间已失去提交资格。");
                            }

                            beforeCommit();
                        }
                        catch
                        {
                            acquired = false;
                            requests.Release();
                            throw;
                        }
                    }

                    void ReleaseCommit()
                    {
                        if (acquired)
                        {
                            acquired = false;
                            requests.Release();
                        }
                    }

                    return await ExecuteArgsUpdateAsync(instance, args, cancellation.Token,
                        AcquireCommitAsync, ReleaseCommit);
                }
            }
            catch (OperationCanceledException)
            {
                return new ArgsUpdateOutcome(ArgsUpdateStatus.CancelledBeforeCommit);
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
                return new ArgsUpdateOutcome(ArgsUpdateStatus.PreparationFailed, error: error);
            }
            finally
            {
                if (acquired)
                {
                    requests.Release();
                }

                updatingArgs.Remove(source);
                EndNavigationRequest();
            }
        }

        private async Task<ArgsUpdateOutcome> ExecuteArgsUpdateAsync<TViewModel, TArgs, TResult>(
            ViewInstance<TViewModel, TArgs, TResult> instance, TArgs args, CancellationToken token,
            Func<CancellationToken, ValueTask> beforeCommit, Action afterCommit)
            where TViewModel : ViewModel
        {
            var outcome = await instance.UpdateArgsAsync(args, token, beforeCommit, afterCommit);
            if (outcome.ViewFaulted)
            {
                CloseFailedArgsUpdate(instance);
            }

            PublishArgsUpdateOutcome(instance, outcome);
            return outcome;
        }

        private void PublishArgsUpdateOutcome(ViewInstance instance, ArgsUpdateOutcome outcome)
        {
            if (outcome.Error != null)
            {
                using (EnterCallback(instance))
                {
                    UIErrors.Report(outcome.Error);
                }
            }

            QueueLifecycleEvent(instance, NavigationEventKind.ArgsUpdateFinished, argsUpdate: outcome);
            DispatchLifecycleEvents();
        }

        internal void CloseFailedArgsUpdate(ViewInstance instance) =>
            CloseFailedTransaction(instance, DismissReason.ArgsUpdateFailed);

        private void CloseFailedTransaction(ViewInstance instance, DismissReason reason)
        {

            Observe(BeginClose(instance, reason));
        }

        internal void MarkInstanceUpdated(ViewInstance instance)
        {
            if (instance.IsActive)
            {
                instance.CommitVersion = ++commitVersion;
            }
        }

        private static ValueTask<ArgsUpdateOutcome> RejectArgsUpdate(ArgsUpdateRejection rejection) =>
            new ValueTask<ArgsUpdateOutcome>(new ArgsUpdateOutcome(ArgsUpdateStatus.Rejected, rejection));
    }
}
