using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        private readonly AsyncLocal<PreparationFrame> guardFrame = new AsyncLocal<PreparationFrame>();
        private LeaveRequest activeLeave;
        private LeaveRequest pendingLeave;
        private bool pumpingLeaves;
        private bool cancellingLeaves;

        private bool IsInGuard => guardFrame.Value != null && guardFrame.Value.Active;

        private ValueTask<TabSelectionResult> RequestLeave(TabContentDefinition source,
                    TabContentDefinition target,
                    bool forceReload,
                    CancellationToken token)
        {
            var existing = pendingLeave ?? activeLeave;
            if (existing != null &&
                !existing.Completion.Task.IsCompleted &&
                ReferenceEquals(existing.Target, target) &&
                existing.ForceReload == forceReload)
            {
                return new ValueTask<TabSelectionResult>(WaitAsync(existing.Completion.Task, token));
            }

            if (token.IsCancellationRequested)
            {
                return Result(TabSelectionStatus.Cancelled);
            }

            CancelLeaveRequests(TabSelectionStatus.Superseded);
            var request = new LeaveRequest
            {
                Source = source,
                Target = target,
                SourceHandle = slot.Current,
                ForceReload = forceReload,
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(scope.Token, token)
            };
            request.Registration = request.Cancellation.Token.Register(()
                => request.Completion.TrySetResult(new TabSelectionResult(scope.IsActive ? TabSelectionStatus.Cancelled : TabSelectionStatus.ParentInactive)));
            pendingLeave = request;
            if (!pumpingLeaves)
            {
                pumpingLeaves = true;
                Observe(work.RunAsync(_ => PumpLeavesAsync()).AsTask());
            }

            return new ValueTask<TabSelectionResult>(request.Completion.Task);
        }

        private void CancelLeaveRequests(TabSelectionStatus status)
        {
            if (cancellingLeaves)
            {
                return;
            }

            cancellingLeaves = true;
            try
            {
                if (pendingLeave != null)
                {
                    var previous = pendingLeave;
                    pendingLeave = null;
                    previous.End(status);
                    previous.Dispose();
                }

                if (activeLeave != null)
                {
                    activeLeave.End(status);
                }
            }
            finally
            {
                cancellingLeaves = false;
            }
        }

        private async ValueTask<bool> PumpLeavesAsync()
        {
            // 离开回调不会在将其入队的 Select 状态修改内部执行。
            await Task.Yield();
            try
            {
                while (pendingLeave != null)
                {
                    var request = activeLeave = pendingLeave;
                    pendingLeave = null;
                    try
                    {
                        var token = request.Cancellation.Token;
                        token.ThrowIfCancellationRequested();
                        var previous = guardFrame.Value;
                        var frame = new PreparationFrame();
                        bool allowed;
                        guardFrame.Value = frame;
                        try
                        {
                            allowed = await request.Source.CanLeaveAsync(new TabLeaveContext(request.Source.Key,
                                request.Target.Key,
                                request.SourceHandle.Model),
                                token);
                        }
                        finally
                        {
                            frame.Active = false;
                            guardFrame.Value = previous;
                        }

                        scope.RequireThread();
                        token.ThrowIfCancellationRequested();
                        if (request.Completion.Task.IsCompleted)
                        {
                            continue;
                        }

                        if (inactive || !scope.IsActive)
                        {
                            request.End(TabSelectionStatus.ParentInactive);
                            continue;
                        }

                        if (!ReferenceEquals(slot.Current, request.SourceHandle) ||
                            !definitions.TryGetValue(request.Target.Key, out var currentTarget) ||
                            !ReferenceEquals(currentTarget, request.Target))
                        {
                            request.End(TabSelectionStatus.Superseded);
                            continue;
                        }

                        if (!allowed)
                        {
                            request.Completion.TrySetResult(new TabSelectionResult(TabSelectionStatus.Rejected,
                                TabRejection.Denied));
                            continue;
                        }

                        // 守卫不持有导航队列许可。只有重新校验
                        // 源与目标身份后，才使用守卫批准结果。
                        activeLeave = null;
                        request.Registration.Dispose();
                        var selection = await SelectCore(request.Target.Key, token, request.ForceReload, true);
                        request.Completion.TrySetResult(selection);
                    }
                    catch (OperationCanceledException)
                    {
                        request.Completion.TrySetResult(new TabSelectionResult(scope.IsActive ? TabSelectionStatus.Cancelled : TabSelectionStatus.ParentInactive));
                    }
                    catch (Exception error)
                    {
                        request.Completion.TrySetResult(new TabSelectionResult(TabSelectionStatus.Failed, error: error));
                    }
                    finally
                    {
                        if (ReferenceEquals(activeLeave, request))
                        {
                            activeLeave = null;
                        }

                        request.Dispose();
                    }
                }
            }
            finally
            {
                pumpingLeaves = false;
            }

            return true;
        }

        private sealed class LeaveRequest : IDisposable
        {
            public TabContentDefinition Source;
            public TabContentDefinition Target;
            public ChildViewHandle SourceHandle;
            public bool ForceReload;
            public CancellationTokenSource Cancellation;
            public CancellationTokenRegistration Registration;
            public readonly TaskCompletionSource<TabSelectionResult> Completion =
                            new TaskCompletionSource<TabSelectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            public void End(TabSelectionStatus status)
            {
                Completion.TrySetResult(new TabSelectionResult(status));
                try
                {
                    Cancellation.Cancel();
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }

            public void Dispose()
            {
                Registration.Dispose();
                Cancellation.Dispose();
            }
        }
    }
}
