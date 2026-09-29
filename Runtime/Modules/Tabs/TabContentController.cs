using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.Tabs
{
    /// <summary>
    /// 协调一个父界面的 Tab 选择、异步准备与子视图生命周期，所有公开操作在所属 UI 线程调用。
    /// 加载期间可保留停用后的旧画面，提交新内容后释放旧内容；不管理导航历史或全局窗口队列。
    /// </summary>
    public sealed partial class TabContentController : ISynchronousDisposable, IAsyncDisposable
    {
        private readonly AsyncLocal<PreparationFrame> preparing = new AsyncLocal<PreparationFrame>();
        private readonly ChildViewScope scope;
        private readonly ChildViewSlot slot;
        private readonly Dictionary<string,
                    TabContentDefinition> definitions = new Dictionary<string,
                    TabContentDefinition>(StringComparer.Ordinal);
        private readonly Lifetime work;
        private readonly SynchronizationContext context = SynchronizationContext.Current;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly CancellationTokenRegistration parentCancellation;
        private readonly TabPendingDisplay pendingDisplay;
        private readonly TabFailureDisplay failureDisplay;
        private readonly TimeSpan indicatorDelay;
        private Operation operation;
        private long version;
        private bool changing;
        private bool publishing;
        private bool inactive;
        private TaskCompletionSource<bool> disposal;

        /// <summary>绑定活动的父 Scope；父级结束时停止选择，并由父级托管异步清理。</summary>
        public TabContentController(ChildViewScope scope,
            IEnumerable<TabContentDefinition> definitions,
            TabPendingDisplay pendingDisplay = TabPendingDisplay.LoadingPlaceholder,
            TimeSpan loadingIndicatorDelay = default,
            TabFailureDisplay failureDisplay = TabFailureDisplay.ErrorPlaceholder,
            TabCacheOptions cacheOptions = null,
            ChildViewPreparationOptions preparationOptions = null)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            scope.RequireActive();
            work = new Lifetime(scope.Mode);
            if (scope.Mode != LifetimeMode.Synchronous)
            {
                cacheMaintenance = Task.CompletedTask;
            }
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            if (!Enum.IsDefined(typeof(TabPendingDisplay), pendingDisplay))
            {
                throw new ArgumentOutOfRangeException(nameof(pendingDisplay));
            }

            if (loadingIndicatorDelay < TimeSpan.Zero || loadingIndicatorDelay.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(loadingIndicatorDelay));
            }

            if (scope.Mode == LifetimeMode.AsyncAllowed &&
                pendingDisplay == TabPendingDisplay.LoadingPlaceholder &&
                loadingIndicatorDelay > TimeSpan.Zero && context == null)
            {
                throw new InvalidOperationException("延迟加载提示需要所属 UI 线程的 SynchronizationContext。");
            }

            if (!Enum.IsDefined(typeof(TabFailureDisplay), failureDisplay))
            {
                throw new ArgumentOutOfRangeException(nameof(failureDisplay));
            }

            this.cacheOptions = cacheOptions ?? TabCacheOptions.Disabled;
            this.failureDisplay = failureDisplay;
            this.pendingDisplay = pendingDisplay;
            indicatorDelay = loadingIndicatorDelay;
            var items = new List<TabItemState>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Null Tab definition.", nameof(definitions));
                }

                RequireDefinitionMode(definition);
                this.definitions.Add(definition.Key, definition);
                items.Add(new TabItemState(definition.Key, definition.Label, definition.IsEnabled()));
            }

            ViewModel = new TabViewModel(items);
            slot = new ChildViewSlot(scope, Mode == LifetimeMode.Synchronous ? null : (Func<ChildViewHandle, ValueTask<bool>>)CacheRetiredAsync,
                preparationOptions, Mode == LifetimeMode.Synchronous ? (Func<ChildViewHandle, bool>)CacheRetiredSynchronous : null);
            slot.CurrentChanged += OnCurrentChanged;
            scope.Own(this);
            parentCancellation = scope.Token.Register(OnParentCancelled);
            if (Mode != LifetimeMode.Synchronous && this.cacheOptions.Capacity > 0 && context != null && !inactive)
            {
                // 延迟恢复必须回到所属线程；纯托管无上下文宿主通过 RefreshCache 显式驱动。
                Observe(work.RunAsync(ScanCacheAsync).AsTask());
            }
        }

        /// <summary>供 TabBar 与内容区域共同观察的只读状态入口。</summary>
        public TabViewModel ViewModel
        {
            get;
        }

        /// <summary>尚未完成准备或迟到清理的隔离数量。</summary>
        public int QuarantinedPreparationCount => slot.QuarantinedPreparationCount;

        /// <summary>
        /// 请求选择指定 Tab。新的选择会取代旧请求；重复等待同一个加载请求时，
        /// 后加入调用者的取消只结束自身等待。forceReload 强制重新准备当前项。
        /// 提交前取消已受理的选择时，尝试重新激活仍保留的旧页，否则清空选择；
        /// 恢复成功仍返回 Cancelled，恢复失败的诊断由结果 Error 和快照提供。
        /// </summary>
        public ValueTask<TabSelectionResult> SelectAsync(string key, CancellationToken cancellationToken = default, bool forceReload = false)
            => SelectCore(key, cancellationToken, forceReload, false);

        private ValueTask<TabSelectionResult> SelectCore(string key,
                    CancellationToken cancellationToken,
                    bool forceReload,
                    bool leaveApproved)
        {
            RequireAsyncAllowed();
            if (inactive || !scope.IsActive)
            {
                return Result(TabSelectionStatus.ParentInactive);
            }

            if (changing ||
                publishing ||
                cancellingLeaves ||
                IsInGuard ||
                IsRetainedCallback ||
                IsCacheCallback ||
                (preparing.Value != null && preparing.Value.Active) ||
                slot.IsExecuting)
            {
                return Rejected(TabRejection.Reentrant);
            }

            if (key == null || !definitions.TryGetValue(key, out var definition))
            {
                return Rejected(TabRejection.UnknownTab);
            }

            changing = true;
            try
            {
                var enabled = definition.IsEnabled();
                ViewModel.SetEnabled(key, enabled);
                if (!enabled)
                {
                    return Rejected(TabRejection.Disabled);
                }

                if (inactive || !scope.IsActive)
                {
                    return Result(TabSelectionStatus.ParentInactive);
                }

                var snapshot = ViewModel.Snapshot;
                if (!leaveApproved)
                {
                    if (slot.Current != null && (forceReload ||
                        snapshot.DisplayedTab != key) && snapshot.DisplayedTab != null && definitions.TryGetValue(snapshot.DisplayedTab, out var source) && source.CanLeaveAsync != null)
                    {
                        return RequestLeave(source, definition, forceReload, cancellationToken);
                    }

                    if (cancellationToken.IsCancellationRequested &&
                        !(snapshot.SelectedTab == key &&
                        snapshot.Phase == TabPhase.Loading))
                    {
                        return Result(TabSelectionStatus.Cancelled);
                    }

                    CancelLeaveRequests(TabSelectionStatus.Superseded);
                }

                if (!forceReload && snapshot.SelectedTab == key)
                {
                    if (snapshot.Phase == TabPhase.Loading && operation != null)
                    {
                        return new ValueTask<TabSelectionResult>(WaitAsync(operation.Completion.Task, cancellationToken));
                    }

                    if (snapshot.Phase == TabPhase.Ready)
                    {
                        return Result(TabSelectionStatus.Ready);
                    }

                    if (snapshot.Phase == TabPhase.Error)
                    {
                        return Result(TabSelectionStatus.Failed, snapshot.Error);
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return Result(TabSelectionStatus.Cancelled);
                }

                if (operation != null && !operation.Completion.Task.IsCompleted)
                {
                    operation.Completion.TrySetResult(new TabSelectionResult(TabSelectionStatus.Superseded));
                    Cancel(operation);
                }

                var next = new Operation
                {
                    Key = key,
                    Version = ++version,
                    Cancellation = CancellationTokenSource.CreateLinkedTokenSource(scope.Token, cancellationToken)
                };
                operation = next;
                try
                {
                    PreparePreviousContent();
                    Publish(new TabSnapshot(key, RetainedDisplayedKey, TabPhase.Loading, null, next.Version,
                        pendingDisplay == TabPendingDisplay.LoadingPlaceholder && indicatorDelay == TimeSpan.Zero));
                    var change = slot.ReplaceAsync((owner, token)
                        => PrepareSelectionAsync(next, definition, owner, token, forceReload), next.Cancellation.Token,
                        _ => Commit(next, definition), () => RequireSelectionCurrent(next, definition));
                    Observe(work.RunAsync(_ => FinishAsync(next, change)).AsTask());
                }
                catch (Exception error)
                {
                    var status = inactive || !scope.IsActive ? TabSelectionStatus.ParentInactive : TabSelectionStatus.Failed;
                    if (IsCurrent(next))
                    {
                        error = ClearFailedContent(error);
                        if (IsCurrent(next))
                        {
                            Publish(new TabSnapshot(key, null, TabPhase.Error, error, next.Version));
                            if (IsCurrent(next))
                            {
                                NotifySelectionFailure(new TabSelectionFailure(key, null, next.Version, error));
                            }
                        }
                    }

                    next.Completion.TrySetResult(new TabSelectionResult(status, error: error));
                    Cancel(next);
                    next.Cancellation.Dispose();
                }

                return new ValueTask<TabSelectionResult>(next.Completion.Task);
            }
            catch (Exception error)
            {
                return Result(TabSelectionStatus.Failed, error);
            }
            finally
            {
                changing = false;
            }
        }

        /// <summary>强制重新加载当前选择，仍经过可用性与离开守卫检查。</summary>
        public ValueTask<TabSelectionResult> RetryAsync(CancellationToken cancellationToken = default)
            => SelectAsync(ViewModel.Snapshot.SelectedTab, cancellationToken, forceReload: true);

        /// <summary>项目状态变化后刷新按钮可用性，不改变当前选择。</summary>
        public void RefreshAvailability()
        {
            scope.RequireThread();
            if (Mode == LifetimeMode.Synchronous)
            {
                RefreshAvailabilitySynchronous();
                return;
            }

            if (changing || publishing || inactive || IsInGuard || cancellingLeaves || slot.IsExecuting || IsRetainedCallback || IsCacheCallback)
            {
                return;
            }

            changing = true;
            try
            {
                foreach (var definition in definitions.Values)
                {
                    ViewModel.SetEnabled(definition.Key, definition.IsEnabled());
                }

                ScheduleCacheInvalidation();
            }
            finally
            {
                changing = false;
            }
        }

        private void Commit(Operation target, TabContentDefinition definition)
        {
            RequireSelectionCurrent(target, definition);

            retained = null;
            displayedDefinition = definition;
            Publish(new TabSnapshot(target.Key, target.Key, TabPhase.Ready, null, target.Version));
        }

        private async ValueTask<bool> FinishAsync(Operation target, ValueTask<ChildViewChangeResult> change)
        {
            var indicator = ShowIndicatorAsync(target, target.Cancellation.Token);
            try
            {
                var result = await change;
                var status = Convert(result.Status);
                var failure = result.Error;
                if (IsCurrent(target))
                {
                    if (status == TabSelectionStatus.Failed || status == TabSelectionStatus.Cancelled)
                    {
                        var resolution = await ResolveUncommittedSelectionAsync(target, failure,
                            status == TabSelectionStatus.Cancelled);
                        status = resolution.Status;
                        failure = resolution.Error;
                    }
                }

                target.Completion.TrySetResult(new TabSelectionResult(status, error: failure));
            }
            catch (Exception error)
            {
                if (IsCurrent(target))
                {
                    error = ClearFailedContent(error);
                    if (IsCurrent(target))
                    {
                        Publish(new TabSnapshot(target.Key, null, TabPhase.Error, error, target.Version));
                    }
                }

                target.Completion.TrySetResult(new TabSelectionResult(TabSelectionStatus.Failed, error: error));
            }
            finally
            {
                Cancel(target);
                await indicator;
                target.Cancellation.Dispose();
            }

            return true;
        }

        private async Task ShowIndicatorAsync(Operation target, CancellationToken token)
        {
            if (pendingDisplay != TabPendingDisplay.LoadingPlaceholder || indicatorDelay == TimeSpan.Zero)
            {
                return;
            }

            try
            {
                await Task.Delay(indicatorDelay, token).ConfigureAwait(false);
                context.Post(_ =>
                {
                    try
                    {
                        scope.RequireThread();
                        if (!token.IsCancellationRequested && IsCurrent(target) &&
                            ViewModel.Snapshot.Phase == TabPhase.Loading && !ViewModel.Snapshot.LoadingIndicatorVisible)
                        {
                            Publish(new TabSnapshot(target.Key, null, TabPhase.Loading, null, target.Version, true));
                        }
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }, null);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        // 迟到的异步完成只能清理自己的资源，不能发布到新选择的表现状态。
        private bool IsCurrent(Operation target) => !inactive && scope.IsActive && ReferenceEquals(operation, target);

        private void Publish(TabSnapshot snapshot)
        {
            var previous = publishing;
            publishing = true;
            try
            {
                ViewModel.Publish(snapshot);
            }
            finally
            {
                publishing = previous;
            }
        }

        private void OnCurrentChanged(ChildViewHandle current)
        {
            if (current == null)
            {
                if (retained != null && (retained.Handle.State == ChildViewState.Closed || retained.Handle.State == ChildViewState.Failed))
                {
                    retained = null;
                    var snapshot = ViewModel.Snapshot;
                    if (!inactive && scope.IsActive && snapshot.Phase == TabPhase.Loading && snapshot.DisplayedTab != null)
                    {
                        Publish(new TabSnapshot(snapshot.SelectedTab, null, snapshot.Phase, snapshot.Error,
                            snapshot.RequestVersion, snapshot.LoadingIndicatorVisible));
                    }
                }

                CancelLeaveRequests(TabSelectionStatus.Superseded);
            }

            if (changing || inactive || !scope.IsActive || current != null || ViewModel.Snapshot.Phase != TabPhase.Ready)
            {
                return;
            }

            Publish(new TabSnapshot(ViewModel.Snapshot.SelectedTab, null, TabPhase.Empty, null, version));
        }

        private void OnParentCancelled()
        {
            if (Thread.CurrentThread.ManagedThreadId == threadId)
            {
                Deactivate();
            }
            else if (Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("Synchronous Tab cancellation must occur on its owning thread.");
            }
            else if (context != null)
            {
                context.Post(_ => Deactivate(), null);
            }
        }

        private void Deactivate()
        {
            if (inactive)
            {
                return;
            }

            inactive = true;
            CancelLeaveRequests(TabSelectionStatus.ParentInactive);
            if (operation != null && !operation.Completion.Task.IsCompleted)
            {
                operation.Completion.TrySetResult(new TabSelectionResult(TabSelectionStatus.ParentInactive));
                Cancel(operation);
            }

            Publish(new TabSnapshot(ViewModel.Snapshot.SelectedTab, null, TabPhase.Inactive, null, ++version));
        }

        /// <summary>停止接收选择并等待子视图与在途工作收尾；重复调用共享同一次清理。</summary>
        public ValueTask DisposeAsync()
        {
            scope.RequireThread();
            if (Mode == LifetimeMode.Synchronous)
            {
                try
                {
                    Dispose();
                    return default;
                }
                catch (Exception error)
                {
                    return new ValueTask(Task.FromException(error));
                }
            }

            if (publishing ||
                changing ||
                cancellingLeaves ||
                IsInGuard ||
                IsRetainedCallback ||
                IsCacheCallback ||
                (preparing.Value != null && preparing.Value.Active) ||
                slot.IsExecuting)
            {
                throw new InvalidOperationException("Cannot await Tab cleanup from its own state or content callback.");
            }

            if (disposal != null)
            {
                return new ValueTask(disposal.Task);
            }

            disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Deactivate();
            _ = DisposeCoreAsync();
            return new ValueTask(disposal.Task);
        }

        private async Task DisposeCoreAsync()
        {
            var errors = new List<Exception>();
            try
            {
                await slot.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                await work.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            await DisposeCacheAsync(errors);
            retained = null;
            displayedDefinition = null;
            slot.CurrentChanged -= OnCurrentChanged;
            parentCancellation.Dispose();
            SelectionFailed = null;
            if (errors.Count == 0)
            {
                disposal.TrySetResult(true);
            }
            else
            {
                disposal.TrySetException(new AggregateException("Tab cleanup failed.", errors));
            }
        }

        private static void Cancel(Operation target)
        {
            try
            {
                target.Cancellation.Cancel();
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }

            if (target.RecoveryCancellation != null)
            {
                try
                {
                    target.RecoveryCancellation.Cancel();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }

        // 共享请求的额外等待者没有请求所有权，取消等待不能取消原始加载。
        private static async Task<TabSelectionResult> WaitAsync(Task<TabSelectionResult> shared, CancellationToken token)
        {
            if (!token.CanBeCanceled || shared.IsCompleted)
            {
                return await shared;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(shared, cancelled.Task) != shared)
                {
                    return new TabSelectionResult(TabSelectionStatus.WaitCancelled);
                }

                return await shared;
            }
        }

        private static TabSelectionStatus Convert(ChildViewChangeStatus status)
        {
            switch (status)
            {
                case ChildViewChangeStatus.Ready:
                    return TabSelectionStatus.Ready;
                case ChildViewChangeStatus.Superseded:
                    return TabSelectionStatus.Superseded;
                case ChildViewChangeStatus.Cancelled:
                    return TabSelectionStatus.Cancelled;
                case ChildViewChangeStatus.ParentInactive:
                    return TabSelectionStatus.ParentInactive;
                default:
                    return TabSelectionStatus.Failed;
            }
        }

        private static ValueTask<TabSelectionResult> Result(TabSelectionStatus status, Exception error = null)
                    => new ValueTask<TabSelectionResult>(new TabSelectionResult(status, error: error));

        private static ValueTask<TabSelectionResult> Rejected(TabRejection reason)
                    => new ValueTask<TabSelectionResult>(new TabSelectionResult(TabSelectionStatus.Rejected, reason));

        private static void Observe(Task task) => _ = ObserveAsync(task);

        private static async Task ObserveAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private sealed class Operation
        {
            public string Key;
            public long Version;
            public CancellationTokenSource Cancellation;
            public CancellationTokenSource RecoveryCancellation;
            public readonly TaskCompletionSource<TabSelectionResult> Completion =
                            new TaskCompletionSource<TabSelectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed class PreparationFrame
        {
            public bool Active = true;
        }
    }
}
