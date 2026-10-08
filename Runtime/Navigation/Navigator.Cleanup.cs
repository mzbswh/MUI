using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private int pendingCleanupCount;
        private readonly int cleanupCapacity;
        private bool hasCleanupFailure;

        /// <summary>准备、关闭决策或关闭清理超时后仍由宿主持有资源的实例数。</summary>
        public int PendingCleanupCount
        {
            get
            {
                AssertThread();
                return pendingCleanupCount + quarantinedPreparations.Count + detachedCloseWaits.Count;
            }
        }

        private bool HasCleanupCapacity
        {
            get
            {
                // 关闭提交已经释放路由额度，但退出动画与清理仍持有真实资源。
                // 从提交起计入清理上限，不能等超时后才限流，否则快速开关会无限堆积实例。
                var retained = 0;
                HashSet<long> retainedViews = null;
                foreach (var instance in entries.Values)
                {
                    if (instance.HasCloseStarted || quarantinedPreparations.Contains(instance.Handle) ||
                        detachedCloseWaits.Contains(instance.Handle))
                    {
                        if (++retained >= cleanupCapacity)
                        {
                            return false;
                        }
                        if (retainedViews == null)
                        {
                            retainedViews = new HashSet<long>();
                        }
                        retainedViews.Add(instance.Handle.Id);
                    }
                }

                // 失败实例移出 entries 后，账本仍持有真实责任；安全重试确认前不得归还名额。
                return CleanupRegistry.GetUnconfirmedOwnerCount(host, retainedViews) < cleanupCapacity - retained;
            }
        }

        private ValueTask<CloseOutcome> WaitForCleanupUntracedAsync(ViewHandle handle, CancellationToken cancellationToken = default)
        {
            AssertThread();
            if (WouldWaitForSelf(handle))
            {
                return new ValueTask<CloseOutcome>(new CloseOutcome(CloseStatus.Reentrant, cleanup: CleanupStatus.Pending));
            }

            if (entries.TryGetValue(handle, out var instance))
            {
                if (dependencyClosures.TryGetValue(handle, out var pendingDependency))
                {
                    return new ValueTask<CloseOutcome>(WaitForCloseAsync(
                        WaitForActualCleanupAsync(instance, pendingDependency), cancellationToken));
                }
                return instance.CleanupCompletion == null
                    ? new ValueTask<CloseOutcome>(new CloseOutcome(CloseStatus.Blocked, cleanup: CleanupStatus.NotRequired))
                    : new ValueTask<CloseOutcome>(WaitForCloseAsync(instance.CleanupCompletion, cancellationToken));
            }

            if (TryGetTerminal(handle, out var result))
            {
                return new ValueTask<CloseOutcome>(result);
            }

            return new ValueTask<CloseOutcome>(new CloseOutcome(
                IsExpired(handle) ? CloseStatus.UnknownOrExpired : CloseStatus.NotFound, cleanup: CleanupStatus.NotRequired));
        }

        private Task<CloseOutcome> BeginCloseForCleanup(ViewInstance instance, DismissReason reason)
        {
            return WaitForActualCleanupAsync(instance, BeginClose(instance, reason));
        }

        /// <summary>强制依赖关闭可能先清理父链，再建立自身的物理清理信号。</summary>
        private static async Task<CloseOutcome> WaitForActualCleanupAsync(ViewInstance instance,
            Task<CloseOutcome> closing)
        {
            var result = await closing;
            return instance.CleanupCompletion == null ? result : await instance.CleanupCompletion;
        }

        private async Task ObserveCloseDeadlineAsync(ViewInstance instance, DismissReason reason,
            Task<CloseOutcome> release, TaskCompletionSource<CloseOutcome> completion,
            long started, CancellationToken token)
        {
            if (release.IsCompleted)
            {
                return;
            }

            try
            {
                var elapsed = TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency);
                var remaining = instance.Route.Policy.CloseTimeout - elapsed;
                await Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, token);
                AssertThread();
                if (token.IsCancellationRequested || release.IsCompleted)
                {
                    return;
                }

                // 超时不移交资源；实例继续留在 entries 并占清理容量，路由额度已在关闭提交时释放。
                instance.CleanupTimedOut = true;
                quarantinedPreparations.Remove(instance.Handle);
                detachedCloseWaits.Remove(instance.Handle);
                ++pendingCleanupCount;
                var timeout = new TimeoutException("View close cleanup exceeded its time budget; resources remain owned until cleanup completes.");
                var pending = new CloseOutcome(CloseStatus.ClosedWithCleanupPending, timeout, CleanupStatus.Pending);
                instance.PublishCloseResult(reason, timeout, CleanupStatus.Pending);
                completion.TrySetResult(pending);
                QueueLifecycleEvent(instance, NavigationEventKind.CloseCleanupPending, reason, pending);
                // 先发布超时结果，再通知合作式 OnCloseAsync，避免内联完成抢先改变业务结果。
                using (EnterCallback(instance))
                {
                    instance.CancelCleanup();
                    UIErrors.Report(timeout);
                }

                DispatchLifecycleEvents();
                using (EnterCallback(instance))
                {
                    instance.PublishResultObservers();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // 实际清理先完成时撤销计时器，不产生超时诊断。
            }
            catch (Exception error)
            {
                // 调度或诊断失败不能遗失仍在运行的真实清理任务。
                UIErrors.Report(error);
            }
        }
    }
}
