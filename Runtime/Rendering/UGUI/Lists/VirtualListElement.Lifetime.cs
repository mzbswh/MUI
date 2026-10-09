using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private LifetimeScope finalNodeCleanup;
        private Exception finalNodeCleanupFailure;

        /// <summary>首次独立释放各项；恢复只确认节点责任，来源或原生退订失败不重复执行。</summary>
        private void ReleaseList()
        {
            if (finalNodeCleanup != null)
            {
                if (!finalNodeCleanup.IsCleanupConfirmed)
                {
                    throw finalNodeCleanupFailure ?? new InvalidOperationException("Virtual list node cleanup is unconfirmed.");
                }
                finalNodeCleanupFailure = null;
                return;
            }
            finalNodeCleanup = new LifetimeScope();
            try
            {
                ReleaseActivationReferences(lifetime);
            }
            catch (Exception error)
            {
                finalNodeCleanup.RecordCleanupFailure(error);
            }

            try
            {
                if (scrollRect != null)
                {
                    scrollRect.onValueChanged.RemoveListener(OnScrolled);
                }
            }
            catch (Exception error)
            {
                finalNodeCleanup.RecordCleanupFailure(error);
            }

            try
            {
                SetStatus(VirtualListStatus.Inactive);
            }
            catch (Exception error)
            {
                finalNodeCleanup.RecordCleanupFailure(error);
            }

            var previousCells = cells.ToArray();
            try
            {
                ClearItemFailures();
            }
            catch (Exception error)
            {
                finalNodeCleanup.RecordCleanupFailure(error);
            }
            cells.Clear();
            templatesByKey.Clear();
            foreach (var cell in previousCells)
            {
                try
                {
                    ReleaseCell(cell);
                }
                catch (Exception error)
                {
                    finalNodeCleanup.RecordCleanupFailureWithConfirmation(error, cell.Element.CaptureNodeCleanupConfirmation());
                }
            }

            try
            {
                // 此作用域只保存已发生的责任确认，不登记异步工作，因此本帧完成。
                var disposal = finalNodeCleanup.DisposeAsync();
                if (!disposal.IsCompleted)
                {
                    throw new InvalidOperationException("List node confirmation must complete synchronously.");
                }
                disposal.GetAwaiter().GetResult();
            }
            catch (Exception error)
            {
                finalNodeCleanupFailure = error;
                throw;
            }
        }

        private void DetachItemsSource()
        {
            var previous = items;
            var previousHandler = itemsChanged;
            items = null;
            itemsChanged = null;
            itemsSubscription = null;
            if (previous != null)
            {
                previous.Changed -= previousHandler;
            }
        }

        /// <summary>
        /// 父激活排空后解除来源及模型引用，缓存只保留可复用的原生节点。
        /// 不重建尺寸、更新选择样式或隐藏节点，退场画面仍由父 View 的保留协议管理。
        /// </summary>
        private void ReleaseActivationReferences(LifetimeScope activation)
        {
            if (!ReferenceEquals(lifetime, activation))
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("虚拟列表的激活清理必须在所属 UI 线程执行。");
            }

            var previousItems = items;
            var previousItemsHandler = itemsChanged;
            // 先使旧回调失效，再调用来源的事件移除器；自定义来源可能在退订时执行项目代码。
            ++itemsAssignmentVersion;
            scope = null;
            lifetime = null;
            items = null;
            itemsChanged = null;
            itemsSubscription = null;
            ++sourceRevision;
            ++revealSourceRevision;
            StopReveal(VirtualListRevealStatus.Inactive);
            ++revealRevision;
            ++selectionRevision;
            hasPublishedViewport = false;
            publishedViewport = default;
            viewportMovementFrame = -1;
            snapshot.Clear();
            // 解绑只解除托管失败记录；退出画面由 View 保留到缓存接管或销毁。
            itemFailures.Clear();
            snapshotVersion = -1;
            measuredItems.Clear();
            measurementCrossExtent = -1;
            rowIndex = null;
            keyIndices.Clear();
            intrinsicKeyIndex = true;
            selectedKey = null;
            selectedIndex = -1;
            first = last = -1;
            pending = null;
            running = false;
            dirty = false;
            Status = VirtualListStatus.Inactive;
            Error = null;
            foreach (var cell in cells)
            {
                cell.Key = null;
            }

            // 正常情况下托管定位已排空；独立持有旧帧信号的观察者也不能无限等待。
            var previousFrame = layoutFrame;
            layoutFrame = null;
            if (previousFrame != null)
            {
                previousFrame.TrySetCanceled();
            }

            var failures = new List<Exception>();
            try
            {
                parentCancellation.Dispose();
            }
            catch (Exception error)
            {
                failures.Add(error);
            }

            try
            {
                if (previousItems != null)
                {
                    previousItems.Changed -= previousItemsHandler;
                }
            }
            catch (Exception error)
            {
                failures.Add(error);
            }

            if (failures.Count != 0)
            {
                throw new AggregateException("虚拟列表激活退订失败。", failures);
            }
        }
    }
}
