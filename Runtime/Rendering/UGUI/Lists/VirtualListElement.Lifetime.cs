using System;
using System.Collections.Generic;
using System.Threading;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        /// <summary>独立释放各项所有权；来源或原生监听退订失败也不能跳过池节点清理。</summary>
        private void ReleaseList()
        {
            var failures = new List<Exception>();
            try
            {
                ReleaseActivationReferences(lifetime);
            }
            catch (Exception error)
            {
                failures.Add(error);
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
                failures.Add(error);
            }

            try
            {
                SetStatus(VirtualListStatus.Inactive);
            }
            catch (Exception error)
            {
                failures.Add(error);
            }

            var previousCells = cells.ToArray();
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
                    failures.Add(error);
                }
            }

            if (failures.Count != 0)
            {
                throw new AggregateException("虚拟列表最终清理失败。", failures);
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
        private void ReleaseActivationReferences(Lifetime activation)
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
            var previousPages = pageSource;
            var previousPageHandler = pageSourceChanged;
            // 先使旧回调失效，再调用来源的事件移除器；自定义来源可能在退订时执行项目代码。
            ++itemsAssignmentVersion;
            scope = null;
            lifetime = null;
            items = null;
            itemsChanged = null;
            itemsSubscription = null;
            pageSource = null;
            pageSourceChanged = null;
            ++pageSourceVersion;
            ++sourceRevision;
            ++revealSourceRevision;
            ++revealRevision;
            ++selectionRevision;
            snapshot.Clear();
            snapshotVersion = -1;
            measuredItems.Clear();
            measurementWidth = -1;
            rowIndex = null;
            keyIndices.Clear();
            intrinsicKeyIndex = true;
            selectedKey = null;
            selectedIndex = -1;
            first = last = -1;
            pending = null;
            running = false;
            automaticPage = null;
            pageRequestError = null;
            pageSourceError = null;
            // 父激活结束后，已发布的同步兼容信号不能等待旧执行栈继续写入新激活。
            var previousRefreshCompletion = synchronousRefreshCompletion;
            synchronousRefreshCompletion = null;
            if (previousRefreshCompletion != null)
            {
                previousRefreshCompletion.TrySetCanceled();
            }

            synchronousRefreshFailure = null;
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

            try
            {
                if (previousPages != null)
                {
                    previousPages.StateChanged -= previousPageHandler;
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
