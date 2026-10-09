using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private bool pumping;

        private ValueTask<OpenOutcome<TResult>> OpenAsyncUntraced<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    CancellationToken cancellationToken,
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
                    var existing = ResolveOpenAdmission(route, args, assigned);
                    if (existing.HasValue)
                    {
                        if (!RequiresOverflowReplacement(route, existing.Value))
                        {
                            if (existing.Value.IsSuccess && entries.TryGetValue(existing.Value.Handle.Identity, out var reused))
                            {
                                await WaitForReadinessAsync(reused, cancellation.Token);
                                return CompletedOpen((ViewInstance<TViewModel, TArgs, TResult>)reused);
                            }

                            return existing.Value;
                        }

                        var source = OldestOpenInstance(route);
                        if (source == null)
                        {
                            // 名额可能全部被准备中的候选持有，不能启动另一份获取或替换未就绪实例。
                            return Reject<TResult>(OpenRejection.Busy);
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
                    // 候选登记即占用实例额度；外部准备期间不占串行提交许可。
                    requests.Release();
                    acquired = false;
                    var preparationFailure = await PrepareCandidateWithDeadlineAsync(instance, cancellation.Token);
                    if (preparationFailure != null)
                    {
                        var closing = QuarantinePreparation(instance, preparationFailure);
                        var status = preparationFailure is TimeoutException ? OpenStatus.PreparationFailed
                            : IsShutdown ? OpenStatus.HostClosed : OpenStatus.CancelledBeforeCommit;
                        return new OpenOutcome<TResult>(status, error: preparationFailure, cleanup: CleanupState(closing));
                    }

                    await requests.WaitAsync(cancellation.Token);
                    acquired = true;
                    AssertThread();
                    cancellation.Token.ThrowIfCancellationRequested();
                    instance.RequirePreparationCurrent();
                    RequireDependenciesCurrent(instance);
                    CommitOpen(instance);
                    committed = true;
                    Activate(instance);
                    ReleaseNavigationQueue(ref acquired);
                    await WaitForReadinessAsync(instance, cancellation.Token);
                    return CompletedOpen(instance);
                }
                catch (NavigationPreparationRejectedException error) when (!committed)
                {
                    ReleaseNavigationQueue(ref acquired);
                    if (instance != null)
                    {
                        await BeginClose(instance, DismissReason.OpenCancelled);
                    }
                    return new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: error.Rejection,
                        error: error, cleanup: InstanceCleanupState(instance));
                }
                catch (OperationCanceledException error)
                {
                    ReleaseNavigationQueue(ref acquired);
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
                    ReleaseNavigationQueue(ref acquired);
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
            // 就绪观察者可以立即关闭页面；Open 的结果仍由首次就绪提交决定，不能读取后来的存活状态。
            if (instance.TryGetReadiness(out var readiness) && readiness.IsReady)
            {
                return new OpenOutcome<TResult>(OpenStatus.Succeeded, instance.TypedHandle, cleanup: InstanceCleanupState(instance));
            }

            var status = instance.Failure != null ? OpenStatus.ActivationFailed : OpenStatus.ClosedBeforeReady;
            return new OpenOutcome<TResult>(status, instance.TypedHandle, error: instance.Failure, cleanup: InstanceCleanupState(instance));
        }

        private static CleanupStatus InstanceCleanupState(ViewInstance instance)
        {
            if (instance == null)
            {
                return CleanupStatus.NotRequired;
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
