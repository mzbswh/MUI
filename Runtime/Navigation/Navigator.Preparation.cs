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

        /// <summary>
        /// 在调用方已经持有的导航事务内准备隐藏候选，不申请队列许可、不提交显示。
        /// false 表示需要异步能力；候选的失败清理由创建它的事务负责。
        /// 只依赖内部候选契约，可处理参数及结果类型不同的页面；不擦除路由的类型约束。
        /// </summary>
        private bool TryPrepareCandidateSynchronously(ViewInstance candidate, int dependencyDepth = 0)
        {
            AssertThread();
            candidate.BeginPreparationExecution(false);
            try
            {
                return TryPrepareCandidateSynchronouslyCore(candidate, dependencyDepth);
            }
            finally
            {
                candidate.EndPreparationExecution();
            }
        }

        private bool TryPrepareCandidateSynchronouslyCore(ViewInstance candidate, int dependencyDepth)
        {
            AssertThread();
            shutdown.Token.ThrowIfCancellationRequested();
            using (EnterCallback(candidate))
            {
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.RequiredDependencies))
                {
                    if (!TryPrepareDependencies(candidate, dependencyDepth, DependencyPlacement.RequiredBefore))
                    {
                        return false;
                    }
                    phase?.Complete();
                }
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ModelCreation))
                {
                    candidate.CreateModel();
                    phase?.Complete();
                }
                if (candidate.RequiresAsync)
                {
                    return false;
                }

                if (candidate.NeedsViewLease)
                {
                    using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ResourceCreation))
                    {
                        try
                        {
                            if (Mode == LifetimeMode.Synchronous)
                            {
                                candidate.AdoptSynchronousLease(synchronousProvider.Create(candidate.Route.Resource));
                            }
                            else
                            {
                                candidate.AdoptLease(provider.Create(candidate.Route.Resource));
                            }
                        }
                        catch (SynchronousResourceLoadException failure)
                        {
                            candidate.RecordPreparationCleanupFailure(failure.CleanupError);
                            throw;
                        }
                        catch (ResourceLoadException failure)
                        {
                            if (Mode == LifetimeMode.Synchronous)
                            {
                                // 同步契约被违反时只保留失败，不访问异步清理接口。
                                candidate.RecordPreparationCleanupFailure(failure);
                            }
                            else
                            {
                                candidate.OwnPreparationCleanup(failure.CleanupCompletion);
                            }
                            throw;
                        }
                        phase?.Complete();
                    }
                }

                shutdown.Token.ThrowIfCancellationRequested();
                candidate.RequirePreparationCurrent();
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ActivationPreparation))
                {
                    candidate.Prepare();
                    if (candidate.RequiresAsync)
                    {
                        return false;
                    }
                    phase?.Complete();
                }
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.AttachedDependencies))
                {
                    if (!TryPrepareDependencies(candidate, dependencyDepth, DependencyPlacement.AttachedAfter))
                    {
                        return false;
                    }
                    phase?.Complete();
                }
                candidate.RequirePreparationCurrent();
                RequireDependenciesCurrent(candidate);
                candidate.PreparationComplete = true;
                return true;
            }
        }

        /// <summary>异步准备也沿用原事务；迟到凭证先接管，再检查取消、线程与候选版本。</summary>
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
                    await PrepareDependenciesAsync(candidate, dependencyDepth, DependencyPlacement.RequiredBefore, cancellationToken);
                    phase?.Complete();
                }
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ModelCreation))
                {
                    candidate.CreateModel();
                    phase?.Complete();
                }
                if (candidate.NeedsViewLease)
                {
                    using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ResourceCreation))
                    {
                        IViewLease acquired;
                        try
                        {
                            acquired = await provider.CreateAsync(candidate.Route.Resource, cancellationToken);
                        }
                        catch (SynchronousResourceLoadException failure)
                        {
                            candidate.RecordPreparationCleanupFailure(failure.CleanupError);
                            throw;
                        }
                        catch (ResourceLoadException failure)
                        {
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
                        candidate.AdoptLease(acquired);
                        phase?.Complete();
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                AssertThread();
                candidate.RequirePreparationCurrent();
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.ActivationPreparation))
                {
                    candidate.Prepare();
                    await candidate.PrepareAsync(cancellationToken);
                    phase?.Complete();
                }
                using (var phase = BeginPreparationTrace(candidate, NavigationPreparationStage.AttachedDependencies))
                {
                    await PrepareDependenciesAsync(candidate, dependencyDepth, DependencyPlacement.AttachedAfter, cancellationToken);
                    phase?.Complete();
                }
                cancellationToken.ThrowIfCancellationRequested();
                candidate.RequirePreparationCurrent();
                RequireDependenciesCurrent(candidate);
                candidate.PreparationComplete = true;
            }
        }
    }
}
