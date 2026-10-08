using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly HashSet<ViewHandle> quarantinedPreparations = new HashSet<ViewHandle>();

        private async Task<Exception> PrepareCandidateWithDeadlineAsync(ViewInstance candidate, CancellationToken token)
        {
            var preparation = PrepareCandidateAsync(candidate, token).AsTask();
            if (preparation.IsCompleted)
            {
                await preparation;
                return null;
            }

            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var elapsed = Task.Delay(candidate.Route.Policy.PrepareTimeout, deadline.Token);
                if (await Task.WhenAny(preparation, elapsed) == preparation || preparation.IsCompleted)
                {
                    deadline.Cancel();
                    await preparation;
                    return null;
                }
            }

            _ = ObserveAbandonedPreparationAsync(preparation);
            if (token.IsCancellationRequested)
            {
                return new OperationCanceledException("View preparation was cancelled before its provider returned.", token);
            }

            return new TimeoutException("View preparation exceeded its time budget; resources remain owned until preparation and cleanup complete.");
        }

        private static async Task ObserveAbandonedPreparationAsync(Task preparation)
        {
            try
            {
                await preparation;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private Task<CloseOutcome> QuarantinePreparation(ViewInstance candidate, Exception failure)
        {
            if (failure is TimeoutException)
            {
                candidate.SetFailure(failure);
            }

            if (!candidate.CleanupTimedOut)
            {
                quarantinedPreparations.Add(candidate.Handle);
            }
            try
            {
                var closing = BeginClose(candidate, failure is TimeoutException
                    ? DismissReason.OpenFailed : DismissReason.OpenCancelled);
                Observe(closing);
                if (failure is TimeoutException)
                {
                    UIErrors.Report(failure);
                }

                return closing;
            }
            catch
            {
                if (!candidate.HasCloseStarted)
                {
                    quarantinedPreparations.Remove(candidate.Handle);
                }

                throw;
            }
        }

        /// <summary>准备隐藏候选；迟到凭证先接管，再检查取消、线程与候选版本。</summary>
        private async ValueTask PrepareCandidateAsync(ViewInstance candidate,
            CancellationToken cancellationToken, int dependencyDepth = 0)
        {
            AssertThread();
            cancellationToken.ThrowIfCancellationRequested();
            candidate.BeginPreparationExecution(true);
            try
            {
                // 原请求取消和当前实例退出都能终止准备；非合作式后端仍须归还迟到凭证。
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, candidate.ActivationToken))
                {
                    await PrepareCandidateAsyncCore(candidate, linked.Token, dependencyDepth);
                }
            }
            finally
            {
                candidate.EndPreparationExecution();
            }
        }

        private async ValueTask PrepareCandidateAsyncCore(ViewInstance candidate,
            CancellationToken cancellationToken, int dependencyDepth)
        {
            using (EnterCallback(candidate))
            {
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.RequiredDependencies))
                {
                    try
                    {
                        await PrepareDependenciesAsync(candidate, dependencyDepth, DependencyPlacement.RequiredBefore, cancellationToken);
                        phase.Complete();
                    }
                    catch (Exception error)
                    {
                        phase.Fail(error);
                        throw;
                    }
                }
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ModelCreation))
                {
                    try
                    {
                        candidate.CreateModel();
                        phase.Complete();
                    }
                    catch (Exception error)
                    {
                        phase.Fail(error);
                        throw;
                    }
                }
                if (candidate.NeedsViewResource)
                {
                    using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ResourceCreation))
                    {
                        IAcquiredView acquired;
                        try
                        {
                            acquired = await provider.AcquireAsync(candidate.Route.Resource, cancellationToken);
                        }
                        catch (ResourceLoadException failure)
                        {
                            phase.Fail(failure);
                            try
                            {
                                await failure.CleanupCompletion;
                            }
                            catch (Exception cleanup)
                            {
                                candidate.RecordPreparationCleanupFailure(cleanup);
                                throw new AggregateException("界面提供方创建与回滚均失败。", failure, cleanup);
                            }
                            var cause = failure.InnerException;
                            while (cause is ResourceLoadException nested)
                            {
                                cause = nested.InnerException;
                            }
                            if (cause is OperationCanceledException cancelled)
                            {
                                throw new OperationCanceledException("界面创建已取消，提供方回滚已结束。", failure, cancelled.CancellationToken);
                            }
                            throw;
                        }
                        catch (Exception error)
                        {
                            phase.Fail(error);
                            throw;
                        }
                        try
                        {
                            candidate.AdoptViewResource(acquired);
                            phase.Complete();
                        }
                        catch (Exception error)
                        {
                            phase.Fail(error);
                            throw;
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                AssertThread();
                candidate.RequirePreparationCurrent();
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ActivationPreparation))
                {
                    try
                    {
                        candidate.Prepare();
                        await candidate.PrepareAsync(cancellationToken);
                        if (candidate.Route.Policy.Modal && candidate.View is IModalView modal)
                        {
                            await modal.PrepareModalBarrierAsync(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                            AssertThread();
                            candidate.RequirePreparationCurrent();
                        }
                        phase.Complete();
                    }
                    catch (Exception error)
                    {
                        phase.Fail(error);
                        throw;
                    }
                }
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.AttachedDependencies))
                {
                    try
                    {
                        await PrepareDependenciesAsync(candidate, dependencyDepth, DependencyPlacement.AttachedAfter, cancellationToken);
                        phase.Complete();
                    }
                    catch (Exception error)
                    {
                        phase.Fail(error);
                        throw;
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                candidate.RequirePreparationCurrent();
                RequireDependenciesCurrent(candidate);
                candidate.PreparationComplete = true;
            }
        }
    }
}
