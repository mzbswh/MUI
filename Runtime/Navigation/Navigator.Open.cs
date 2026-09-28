using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private bool pumping;

        private OpenOutcome<TResult> OpenUntraced<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    TViewModel assignedViewModel, NavigationTraceOperation trace)
                    where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (IsShutdown)
            {
                return new OpenOutcome<TResult>(OpenStatus.HostClosed);
            }

            if (IsReentrant)
            {
                return Reject<TResult>(OpenRejection.Reentrant);
            }

            if (route.Preparation == PreparationMode.AsyncOnly ||
                (Mode == LifetimeMode.Synchronous && !route.SupportsSynchronousLifecycle))
            {
                return Reject<TResult>(OpenRejection.RequiresAsync);
            }

            if (pending != 0 || !requests.Wait(0))
            {
                return Reject<TResult>(OpenRejection.Busy);
            }

            BeginNavigationRequest();
            ViewInstance<TViewModel, TArgs, TResult> instance = null;
            var committed = false;
            try
            {
                Register(route);
                var existing = Existing(route, args, assignedViewModel);
                if (existing.HasValue)
                {
                    if (RequiresOverflowReplacement(route, existing.Value))
                    {
                        if (Mode != LifetimeMode.Synchronous)
                        {
                            return Reject<TResult>(OpenRejection.RequiresAsync);
                        }
                        var source = OldestOpenInstance(route);
                        if (source == null)
                        {
                            return existing.Value;
                        }
                        if (WouldWaitForSelf(source.Handle))
                        {
                            return Reject<TResult>(OpenRejection.Reentrant);
                        }
                        return FromOverflowReplacement(source.Handle,
                            ReplaceSynchronousCore(source, route, args, assignedViewModel, trace));
                    }

                    return existing.Value;
                }

                SyncCreateAvailability available;
                using (EnterCallback(null))
                {
                    available = HasCachedContent(route, assignedViewModel) ? SyncCreateAvailability.Available
                        : Mode == LifetimeMode.Synchronous ? synchronousProvider.GetSyncAvailability(route.Resource)
                        : provider.GetSyncAvailability(route.Resource);
                }
                if (available != SyncCreateAvailability.Available)
                {
                    return Reject<TResult>(available == SyncCreateAvailability.RequiresPreload ? OpenRejection.RequiresPreload : OpenRejection.SyncCreationUnsupported);
                }

                instance = NewInstance(route, args, assignedViewModel);
                AssignPreparationTrace(instance, trace);
                if (!TryPrepareCandidateSynchronously(instance))
                {
                    var cleanup = CloseAfterFailure(instance, DismissReason.OpenCancelled);
                    return Reject<TResult>(OpenRejection.RequiresAsync, cleanup);
                }

                shutdown.Token.ThrowIfCancellationRequested();
                instance.RequirePreparationCurrent();
                RequireDependenciesCurrent(instance);
                CommitOpen(instance);
                committed = true;
                Activate(instance);
                return CompletedOpen(instance);
            }
            catch (NavigationPreparationRejectedException error) when (!committed)
            {
                if (instance != null)
                {
                    CloseAfterFailure(instance, DismissReason.OpenCancelled);
                }
                return new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: error.Rejection,
                    error: error, cleanup: InstanceCleanupState(instance));
            }
            catch (OperationCanceledException error)
            {
                if (committed && instance != null)
                {
                    instance.SetFailure(error);
                }

                if (instance != null)
                {
                    CloseAfterFailure(instance, DismissReason.OpenCancelled);
                }

                return new OpenOutcome<TResult>(committed ? OpenStatus.ActivationFailed : IsShutdown ? OpenStatus.HostClosed : OpenStatus.CancelledBeforeCommit, committed ? instance.TypedHandle : default, error: error, cleanup: InstanceCleanupState(instance));
            }
            catch (Exception error)
            {
                if (instance != null)
                {
                    instance.SetFailure(error);
                    CloseAfterFailure(instance, DismissReason.OpenFailed);
                }

                UIErrors.Report(error);
                return new OpenOutcome<TResult>(committed ? OpenStatus.ActivationFailed : OpenStatus.PreparationFailed, committed ? instance.TypedHandle : default, error: error, cleanup: InstanceCleanupState(instance));
            }
            finally
            {
                requests.Release();
                EndNavigationRequest();
            }
        }

        private ValueTask<OpenOutcome<TResult>> OpenAsyncUntraced<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    CancellationToken cancellationToken,
                    TViewModel assignedViewModel, NavigationTraceOperation trace)
                    where TViewModel : ViewModel
        {
            AssertThread();
            RequireAsyncNavigation();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (IsShutdown)
            {
                return new ValueTask<OpenOutcome<TResult>>(new OpenOutcome<TResult>(OpenStatus.HostClosed));
            }

            if (IsReentrant)
            {
                return new ValueTask<OpenOutcome<TResult>>(Reject<TResult>(OpenRejection.Reentrant));
            }

            if (pending >= queueCapacity)
            {
                return new ValueTask<OpenOutcome<TResult>>(Reject<TResult>(OpenRejection.Busy));
            }

            BeginNavigationRequest();
            return new ValueTask<OpenOutcome<TResult>>(OpenCoreAsync(route, args, cancellationToken, assignedViewModel, null, trace: trace));
        }

        private async Task<OpenOutcome<TResult>> OpenCoreAsync<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    CancellationToken token,
                    TViewModel assigned,
                    ViewInstance deferredSource,
                    Task<bool> startSignal = null, NavigationTraceOperation trace = default)
                    where TViewModel : ViewModel
        {
            var acquired = false;
            var committed = false;
            ViewInstance<TViewModel, TArgs, TResult> instance = null;
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, shutdown.Token))
            {
                try
                {
                    await requests.WaitAsync(cancellation.Token);
                    acquired = true;
                    if (startSignal != null)
                    {
                        await AsyncWait.WithCancellation(startSignal, cancellation.Token);
                    }

                    AssertThread();
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (deferredSource != null && !deferredSource.IsActive)
                    {
                        return new OpenOutcome<TResult>(OpenStatus.CancelledBeforeCommit);
                    }

                    Register(route);
                    var existing = Existing(route, args, assigned);
                    if (existing.HasValue)
                    {
                        if (!RequiresOverflowReplacement(route, existing.Value))
                        {
                            if (existing.Value.IsSuccess && entries.TryGetValue(existing.Value.Handle.Identity, out var reused))
                            {
                                await WaitForEnterAsync(reused, cancellation.Token);
                                return CompletedOpen((ViewInstance<TViewModel, TArgs, TResult>)reused);
                            }

                            return existing.Value;
                        }

                        var source = OldestOpenInstance(route);
                        if (source == null)
                        {
                            return existing.Value;
                        }

                        if (WouldWaitForSelf(source.Handle))
                        {
                            return Reject<TResult>(OpenRejection.Reentrant);
                        }

                        if (!CanReplace(source) || !replacing.Add(source.Handle))
                        {
                            return Reject<TResult>(OpenRejection.Busy);
                        }

                        // 转交已持有的队列许可，但保留当前请求的外层计数。
                        // 不需要第二个队列名额或额外请求容量。
                        acquired = false;
                        var replacement = await ReplaceCoreAsync(source, route, args, cancellation.Token, assigned, queueAcquired: true, ownsRequest: false, trace: trace);
                        return FromOverflowReplacement(source.Handle, replacement);
                    }

                    instance = NewInstance(route, args, assigned);
                    AssignPreparationTrace(instance, trace);
                    await PrepareCandidateAsync(instance, cancellation.Token);

                    AssertThread();
                    cancellation.Token.ThrowIfCancellationRequested();
                    instance.RequirePreparationCurrent();
                    RequireDependenciesCurrent(instance);
                    CommitOpen(instance);
                    committed = true;
                    Activate(instance);
                    await WaitForEnterAsync(instance, cancellation.Token);
                    return CompletedOpen(instance);
                }
                catch (NavigationPreparationRejectedException error) when (!committed)
                {
                    if (instance != null)
                    {
                        await BeginClose(instance, DismissReason.OpenCancelled);
                    }
                    return new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: error.Rejection,
                        error: error, cleanup: InstanceCleanupState(instance));
                }
                catch (OperationCanceledException error)
                {
                    if (committed && instance != null)
                    {
                        instance.SetFailure(error);
                    }

                    if (instance != null)
                    {
                        await BeginClose(instance, DismissReason.OpenCancelled);
                    }

                    return new OpenOutcome<TResult>(committed ? OpenStatus.ActivationFailed : IsShutdown ? OpenStatus.HostClosed : OpenStatus.CancelledBeforeCommit, committed ? instance.TypedHandle : default, error: error, cleanup: InstanceCleanupState(instance));
                }
                catch (Exception error)
                {
                    if (instance != null)
                    {
                        instance.SetFailure(error);
                        await BeginClose(instance, DismissReason.OpenFailed);
                    }

                    UIErrors.Report(error);
                    return new OpenOutcome<TResult>(committed ? OpenStatus.ActivationFailed : OpenStatus.PreparationFailed, committed ? instance.TypedHandle : default, error: error, cleanup: InstanceCleanupState(instance));
                }
                finally
                {
                    if (acquired)
                    {
                        requests.Release();
                    }

                    EndNavigationRequest();
                }
            }
        }

        private static OpenOutcome<TResult> CompletedOpen<TViewModel, TArgs, TResult>(ViewInstance<TViewModel, TArgs, TResult> instance)
                    where TViewModel : ViewModel
        {
            var status = instance.Failure != null ? OpenStatus.ActivationFailed : instance.State == ViewState.Open ? OpenStatus.Succeeded : OpenStatus.ClosedBeforeReady;
            return new OpenOutcome<TResult>(status, instance.TypedHandle, error: instance.Failure, cleanup: InstanceCleanupState(instance));
        }

        private static CleanupStatus InstanceCleanupState(ViewInstance instance)
        {
            if (instance == null)
            {
                return CleanupStatus.NotRequired;
            }
            if (instance.Mode == LifetimeMode.Synchronous)
            {
                return instance.CompletedCloseOutcome.HasValue
                    ? instance.CompletedCloseOutcome.Value.Cleanup : CleanupStatus.NotRequired;
            }
            return CleanupState(instance.Closing);
        }

        private static CleanupStatus CleanupState(Task<CloseOutcome> task)
        {
            if (task == null)
            {
                return CleanupStatus.NotRequired;
            }

            if (!task.IsCompleted)
            {
                return CleanupStatus.Pending;
            }

            // 只检查已完成任务；同步 Open 不能阻塞等待清理。
            return task.IsCompletedSuccessfully ? task.GetAwaiter().GetResult().Cleanup : CleanupStatus.Failed;
        }

        private void CommitOpen(ViewInstance instance)
        {
            // 这里只修改内部状态，不调用生命周期、绑定设置器或渲染器。
            CommitDependencies(instance, DependencyPlacement.RequiredBefore);
            instance.State = ViewState.Open;
            instance.CommitVersion = ++commitVersion;
            instance.Order = ++order;
            activeOrder.Add(instance);
            if (instance.Route.Policy.EnterHistory && ownership.HasExplicitOwner(instance.Handle))
            {
                history.Add(instance.Handle);
            }

            QueueLifecycleEvent(instance, NavigationEventKind.OpenCommitted);
            CommitDependencies(instance, DependencyPlacement.AttachedAfter);
        }

        private void Activate(ViewInstance instance)
        {
            presentationDeferrals++;
            try
            {
                ActivateDependencies(instance, DependencyPlacement.RequiredBefore);
                DispatchLifecycleEvents();
                // 观察者和绑定设置器可能关闭本页面或其他页面。
                // 绑定提交完成前，任何此类回调都不能使候选可见。
                if (!instance.IsActive)
                {
                    return;
                }

                using (EnterCallback(instance))
                {
                    instance.ActivateBindings();
                }

                if (!instance.IsActive)
                {
                    return;
                }

                StartEnter(instance);
                if (!instance.IsActive)
                {
                    return;
                }

                instance.ActivationCommitted = true;
                ActivateDependencies(instance, DependencyPlacement.AttachedAfter);
                RefreshTickRegistration(instance);
            }
            finally
            {
                presentationDeferrals--;
                RecomputePresentation();
            }
        }

        public PostOpenStatus PostOpen<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route, TArgs args)
                    where TViewModel : ViewModel
        {
            AssertThread();
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (IsShutdown)
            {
                return PostOpenStatus.HostClosed;
            }

            if (pending >= queueCapacity)
            {
                return PostOpenStatus.Busy;
            }

            if (Mode == LifetimeMode.Synchronous)
            {
                if (posted.Count >= queueCapacity)
                {
                    return PostOpenStatus.Busy;
                }

                var origin = CurrentCallback?.Source;
                posted.Enqueue(() =>
                {
                    if (origin == null || origin.IsActive)
                    {
                        Open(route, args);
                    }
                });
                return PostOpenStatus.Accepted;
            }

            var current = CurrentCallback;
            var source = current != null ? current.Source : null;
            BeginNavigationRequest();
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            posted.Enqueue(() => start.TrySetResult(true));
            // 立即预留队列位置；Pump 只在回调返回后允许排队工作开始。
            var trace = BeginOperationTrace(route.Key, "PostOpen");
            using (EnterOperationTrace(trace))
            {
                var operation = OpenCoreAsync(route, args, default, null, source, start.Task, trace);
                _ = ObserveOpen(trace.Id == 0 ? operation :
                    ObserveTracedOpenAsync(new ValueTask<OpenOutcome<TResult>>(operation), trace).AsTask());
            }
            return PostOpenStatus.Accepted;
        }

        /// <summary>由 UIHost 每帧调用一次；派发导航与子请求，不受业务 Tick 暂停策略影响。</summary>
        public void Pump()
        {
            AssertThread();
            if (IsShutdown || IsReentrant || pumping)
            {
                return;
            }

            pumping = true;
            try
            {
                MaintainNativeFocus();
                var count = posted.Count;
                for (var i = 0; i < count && posted.Count != 0 && !IsShutdown; ++i)
                {
                    posted.Dequeue()();
                }

                PumpChildRequests();
            }
            finally
            {
                pumping = false;
            }
        }

        private static async Task ObserveOpen<TResult>(Task<OpenOutcome<TResult>> completion)
        {
            try
            {
                // OpenCore 负责报告意外失败；取消和拒绝属于正常结果。
                await completion;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
