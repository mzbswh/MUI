using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private const int SnapshotOwnerLimit = 128;

        /// <summary>
        /// 直接捕获导航账本；不创建任务，不读取渲染器或业务属性。
        /// 实例截断时可用 TryGetInstanceSnapshot 定位指定句柄；不从诊断入口强制重算表现。
        /// </summary>
        public NavigationSnapshot CaptureSnapshot(int maxInstances = 256)
        {
            AssertThread();
            if (maxInstances < 1 || maxInstances > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInstances), "快照实例上限必须在 1 至 4096 之间。");
            }
            var selected = new List<ViewInstance>(Math.Min(maxInstances, entries.Count));
            foreach (var instance in entries.Values)
            {
                if (selected.Count == maxInstances)
                {
                    break;
                }
                selected.Add(instance);
            }
            selected.Sort((left, right) => left.Handle.Id.CompareTo(right.Handle.Id));
            var indices = new Dictionary<ViewHandle, (int presentation, int history)>(selected.Count);
            foreach (var instance in selected)
            {
                indices.Add(instance.Handle, (-1, -1));
            }
            // 只为采集集合保留索引，不因全宿主存在大量条目而分配无界索引表。
            for (var i = 0; i < activeOrder.Count; ++i)
            {
                var handle = activeOrder[i].Handle;
                if (indices.TryGetValue(handle, out var index))
                {
                    indices[handle] = (i, index.history);
                }
            }
            for (var i = 0; i < history.Count; ++i)
            {
                if (indices.TryGetValue(history[i], out var index))
                {
                    indices[history[i]] = (index.presentation, i);
                }
            }
            var snapshots = new ViewInstanceSnapshot[selected.Count];
            for (var i = 0; i < selected.Count; ++i)
            {
                var instance = selected[i];
                var index = indices[instance.Handle];
                snapshots[i] = CaptureInstanceSnapshot(instance, index.presentation, index.history);
            }
            var historyLength = Math.Min(history.Count, maxInstances);
            var recentHistory = new ViewHandle[historyLength];
            history.CopyTo(history.Count - historyLength, recentHistory, 0, historyLength);
            return new NavigationSnapshot(IsShutdown, commitVersion,
                !recomputingPresentation && !presentationDirty && presentationDeferrals == 0 && !IsReentrant,
                focused, entries.Count, snapshots, history.Count, recentHistory, pending, posted.Count,
                cachedContents.Count, retiringCachedViews, preloadReservations, PendingCleanupCount,
                hasUnconfirmedCleanup, DroppedLifecycleEventCount);
        }

        /// <summary>取得仍在账本中的实例，包括准备中和等待清理的实例；终态历史不伪造活动快照。</summary>
        public bool TryGetInstanceSnapshot(ViewHandle handle, out ViewInstanceSnapshot snapshot)
        {
            AssertThread();
            if (!entries.TryGetValue(handle, out var instance))
            {
                snapshot = null;
                return false;
            }
            snapshot = CaptureInstanceSnapshot(instance, activeOrder.IndexOf(instance), history.IndexOf(handle));
            return true;
        }

        private ViewInstanceSnapshot CaptureInstanceSnapshot(ViewInstance instance, int presentationIndex, int historyIndex)
        {
            var operations = ViewOperationFlags.None;
            var executingCommands = instance.ExecutingBindingCommandCount;
            if (instance.IsPreparing)
            {
                operations |= ViewOperationFlags.Preparing;
            }
            if (instance.IsUpdatingArgs)
            {
                operations |= ViewOperationFlags.UpdatingArgs;
            }
            if (instance.IsRebinding)
            {
                operations |= ViewOperationFlags.Rebinding;
            }
            if (executingCommands != 0)
            {
                operations |= ViewOperationFlags.ExecutingCommand;
            }
            if (instance.EnterPending)
            {
                operations |= ViewOperationFlags.Entering;
            }
            if (instance.ExitPending)
            {
                operations |= ViewOperationFlags.Exiting;
            }
            if (instance.CloseRequest != null)
            {
                operations |= ViewOperationFlags.CloseRequested;
            }
            if (instance.HasCloseStarted)
            {
                operations |= ViewOperationFlags.Closing;
            }
            if (instance.CleanupTimedOut)
            {
                operations |= ViewOperationFlags.CleanupTimedOut;
            }
            if (retiringDependencies.Contains(instance.Handle))
            {
                operations |= ViewOperationFlags.DependencyExit;
            }
            return new ViewInstanceSnapshot(instance, presentationIndex, historyIndex,
                ownership.HasExplicitOwner(instance.Handle), ownership.OwnerCount(instance.Handle),
                ownership.CaptureOwners(instance.Handle, SnapshotOwnerLimit), ownership.CaptureDependencies(instance.Handle), operations, executingCommands);
        }
    }
}
