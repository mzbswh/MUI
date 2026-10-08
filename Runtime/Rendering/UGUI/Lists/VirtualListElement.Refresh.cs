using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.ChildViews;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    // 刷新调度、视口条目准备及节点复用；配置和数据快照入口位于主文件。
    public sealed partial class VirtualListElement
    {
        private void RequestRefresh(bool recover = false)
        {
            if (preparedRebind != null || !initialized || scope == null || !scope.IsActive || (Error != null && !recover))
            {
                return;
            }

            if (!running && !dirty && first >= 0 && !recover)
            {
                try
                {
                    Layout.GetRange(snapshot.Count, ScrollOffset, ViewportExtent, out var start, out var end);
                    if (start == first && end == last)
                    {
                        return;
                    }
                }
                catch (Exception failure)
                {
                    pending = Task.FromException(failure);

                    SetStatus(VirtualListStatus.Error, failure);
                    throw;
                }
            }

            dirty = true;
            if (running)
            {
                if (recover && Error != null)
                {
                    SetStatus(VirtualListStatus.Loading);
                }

                return;
            }

            StartRefresh();
        }

        private void StartRefresh()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshTask = completion.Task;
            var activation = lifetime;
            pending = refreshTask;
            running = true;
            try
            {
                // 在状态观察者可调用 RetryAsync/PendingChange 前先发布操作。
                var operation = activation.RunAsync(async token =>
                {
                    try
                    {
                        SetStatus(VirtualListStatus.Loading);
                        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                        if (eventSystem != null && eventSystem.alreadySelecting)
                        {
                            // 异步路径先发布本轮完成信号，再让出选择回调，PendingChange 不得返回旧轮次的结果。
                            await WaitForLayoutFrameAsync(token);
                        }

                        var remaining = (long)maxCells + 8;
                        while (IsRefreshOperationCurrent(activation, refreshTask) && scope != null && scope.IsActive)
                        {
                            token.ThrowIfCancellationRequested();
                            if (Error != null)
                            {
                                throw Error;
                            }

                            if (--remaining < 0)
                            {
                                throw new InvalidOperationException("Virtual list refresh did not stabilize within its callback budget.");
                            }

                            if (dirty)
                            {
                                dirty = false;
                                await RefreshAsync();
                                continue;
                            }

                            SetStatus(snapshot.Count == 0 ? VirtualListStatus.Empty : VirtualListStatus.Ready);
                            if (!IsRefreshOperationCurrent(activation, refreshTask))
                            {
                                break;
                            }

                            // Ready/Empty 观察者可能同步提交另一个来源。
                            if (!dirty && Error == null)
                            {
                                break;
                            }

                            if (Error == null)
                            {
                                SetStatus(VirtualListStatus.Loading);
                            }
                        }

                        token.ThrowIfCancellationRequested();
                        if (IsRefreshOperationCurrent(activation, refreshTask) && (scope == null || !scope.IsActive))
                        {
                            SetStatus(VirtualListStatus.Inactive);
                        }

                        return true;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        if (IsRefreshOperationCurrent(activation, refreshTask))
                        {
                            SetStatus(VirtualListStatus.Inactive);
                        }

                        throw;
                    }
                    catch (Exception failure)
                    {
                        if (IsRefreshOperationCurrent(activation, refreshTask))
                        {
                            SetStatus(VirtualListStatus.Error, failure);
                        }

                        throw;
                    }
                    finally
                    {
                        if (IsRefreshOperationCurrent(activation, refreshTask))
                        {
                            running = false;
                        }
                    }
                }).AsTask();
                _ = CompleteRefreshAsync(operation, completion);
            }
            catch (Exception failure)
            {
                completion.TrySetException(failure);
                if (IsRefreshOperationCurrent(activation, refreshTask))
                {
                    running = false;
                    SetStatus(VirtualListStatus.Error, failure);
                }
            }

            // 刷新回调可能结束激活或启动新一轮操作，只观察本轮已发布的完成边界。
            if (refreshTask.IsCompleted)
            {
                refreshTask.GetAwaiter().GetResult();
            }
            else
            {
                _ = ObserveAsync(refreshTask);
            }
        }

        private bool IsRefreshOperationCurrent(LifetimeScope activation, Task refreshTask) =>
            IsAlive && ReferenceEquals(lifetime, activation) && ReferenceEquals(pending, refreshTask);

        private static async Task CompleteRefreshAsync(Task operation, TaskCompletionSource<bool> completion)
        {
            try
            {
                await operation;
                completion.TrySetResult(true);
            }
            catch (OperationCanceledException error)
            {
                completion.TrySetCanceled(error.CancellationToken);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }

        private async Task RefreshAsync()
        {
            using (var batches = RefreshCells().GetEnumerator())
            {
                while (true)
                {
                    Task[] batch;
                    // 每段只计所属帧的同步 CPU；标记不得跨越外部等待或 iterator 的 yield。
                    using (refreshMarker.Auto())
                    {
                        if (!batches.MoveNext())
                        {
                            break;
                        }

                        batch = batches.Current;
                    }

                    if (batch.Length == 1)
                    {
                        await batch[0];
                    }
                    else
                    {
                        await Task.WhenAll(batch);
                    }
                }
            }
        }

        // 共用视口协调与单元所有权；每次产出是一组嵌套绑定的完成边界。
        private IEnumerable<Task[]> RefreshCells()
        {
            var revision = sourceRevision;
            var activation = lifetime;
            int start, end;
            using (rangeMarker.Auto())
            {
                Layout.GetRange(snapshot.Count, ScrollOffset, ViewportExtent, out start, out end);
            }
            if (start == first && end == last)
            {
                yield break;
            }

            if (end - start > maxCells)
            {
                throw new InvalidOperationException("Viewport exceeds the configured virtual list cell capacity.");
            }

            if (!IsRefreshCurrent(revision, activation))
            {
                InvalidateAbandonedRefreshRange(activation);
                yield break;
            }

            var draining = new List<Task>();
            for (var cellIndex = 0; cellIndex < cells.Count; ++cellIndex)
            {
                var cell = cells[cellIndex];
                if (cell.Key == null)
                {
                    continue;
                }

                var found = keyIndices.TryGetValue(cell.Key, out var itemIndex);
                if (!IsRefreshCurrent(revision, activation))
                {
                    break;
                }

                var retained = cell.SourceGeneration == revealSourceRevision && found && itemIndex >= start && itemIndex < end &&
                    string.Equals(snapshot[itemIndex].TemplateKey, cell.TemplateKey, StringComparison.Ordinal);
                if (!retained)
                {
                    View.ReleaseFocusWithin(cell.Root);
                    if (!IsRefreshCurrent(revision, activation))
                    {
                        InvalidateAbandonedRefreshRange(activation);
                        yield break;
                    }

                    cell.Root.gameObject.SetActive(false);
                    if (!IsRefreshCurrent(revision, activation))
                    {
                        break;
                    }

                    cell.Element.ViewModel = null;
                    if (!IsRefreshCurrent(revision, activation))
                    {
                        InvalidateAbandonedRefreshRange(activation);
                        yield break;
                    }

                    draining.Add(((IChildViewElement)cell.Element).Preparation);

                    cell.Key = null;
                    if (!IsRefreshCurrent(revision, activation))
                    {
                        break;
                    }
                }
            }

            if (draining.Count != 0)
            {
                yield return draining.ToArray();
            }

            if (!IsRefreshCurrent(revision, activation))
            {
                InvalidateAbandonedRefreshRange(activation);
                yield break;
            }

            var preparedCell = new Task[1];
            for (var i = start; i < end; i++)
            {
                preparedCell[0] = PrepareItemAsync(snapshot[i], i, revision, activation);
                yield return preparedCell;
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    yield break;
                }
            }

            RefreshFailurePlaceholders(start, end, revision, activation);
            if (!IsRefreshCurrent(revision, activation))
            {
                InvalidateAbandonedRefreshRange(activation);
                yield break;
            }

            // 只保留当前视口的工作集，不因过去更大视口而一直持有峰值资源。
            for (var i = cells.Count - 1; i >= 0; --i)
            {
                if (cells[i].Key == null)
                {
                    var cell = cells[i];
                    cells.RemoveAt(i);
                    ReleaseCell(cell);
                    if (!IsRefreshCurrent(revision, activation))
                    {
                        InvalidateAbandonedRefreshRange(activation);
                        yield break;
                    }
                }
            }

            first = start;
            last = end;
        }

        /// <summary>条目准备失败只撤销该条目，其他条目沿原刷新继续。</summary>
        private async Task PrepareItemAsync(VirtualListItem item, int i, long revision, LifetimeScope activation)
        {
            if (GetItemFailure(item.Key) != null)
            {
                return;
            }

            Cell cell = null;
            try
            {
                cell = FindReusableCell(item, revision, activation);
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    return;
                }

                if (cell == null)
                {
                    // 已排空的异模板单元不能换绑成新模板；先移除空闲单元，避免按模板累积历史池。
                    var unused = cells.Find(candidate => candidate.Key == null);
                    if (unused != null)
                    {
                        cells.Remove(unused);
                        ReleaseCell(unused);
                    }

                    if (!IsRefreshCurrent(revision, activation))
                    {
                        InvalidateAbandonedRefreshRange(activation);
                        return;
                    }

                    cell = CreateCell(item.TemplateKey, revision, activation);
                    if (cell == null)
                    {
                        InvalidateAbandonedRefreshRange(activation);
                        return;
                    }

                    cells.Add(cell);
                }

                cell.Key = item.Key;
                cell.SourceGeneration = revealSourceRevision;
                PositionCell(cell.Root, i, EffectiveExtent(item));
                // 原生尺寸回调可以同步换来源或关闭父级，旧快照不得继续启动业务绑定。
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    return;
                }

                using (itemBindingMarker.Auto())
                {
                    cell.Element.ViewModel = item.ViewModel;
                }
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    return;
                }

                await ((IChildViewElement)cell.Element).Preparation;

                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    return;
                }

                if (cell.Selection != null)
                {
                    var selection = selectionRevision;
                    var selected = Equals(cell.Key, selectedKey);
                    if (!IsRefreshCurrent(revision, activation))
                    {
                        InvalidateAbandonedRefreshRange(activation);
                        return;
                    }

                    if (selection == selectionRevision)
                    {
                        cell.Selection.SetSelected(selected);
                    }
                }

                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    return;
                }

                cell.Root.gameObject.SetActive(true);
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    return;
                }
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                if (cell != null)
                {
                    cells.Remove(cell);
                    try
                    {
                        cell.Root.gameObject.SetActive(false);
                        cell.Element.ViewModel = null;
                        await ((IChildViewElement)cell.Element).Preparation;
                    }
                    catch (Exception cleanup)
                    {
                        errors.Add(cleanup);
                    }

                    try
                    {
                        ReleaseCell(cell);
                    }
                    catch (Exception cleanup)
                    {
                        errors.Add(cleanup);
                    }
                }

                var error = errors.Count == 1 ? failure : new AggregateException("Virtual list item preparation and cleanup failed.", errors);
                if (IsRefreshCurrent(revision, activation))
                {
                    RecordItemFailure(item, error);
                }
                else if (!(error is OperationCanceledException))
                {
                    UIErrors.Report(error);
                }
            }
        }

        private Cell FindReusableCell(VirtualListItem item, long revision, LifetimeScope activation)
        {
            Cell unused = null;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var matches = Equals(cell.Key, item.Key);
                if (!IsRefreshCurrent(revision, activation))
                {
                    return null;
                }

                if (matches)
                {
                    return cell;
                }

                if (unused == null && cell.Key == null &&
                    string.Equals(cell.TemplateKey, item.TemplateKey, StringComparison.Ordinal))
                {
                    unused = cell;
                }
            }

            // 已显示同一键的单元优先；找不到时才复用第一个匹配模板的空闲单元。
            return unused;
        }

        private bool IsRefreshCurrent(long revision, LifetimeScope activation) =>
            IsAlive && scope != null && scope.IsActive && ReferenceEquals(lifetime, activation) &&
            !activation.IsEnded && revision == sourceRevision && !dirty && Error == null;

        private void InvalidateAbandonedRefreshRange(LifetimeScope activation)
        {
            if (IsAlive && ReferenceEquals(lifetime, activation))
            {
                first = last = -1;
            }
        }

        private Cell CreateCell(string templateKey, long revision, LifetimeScope activation, Func<bool> isCurrent = null)
        {
            bool IsCurrent() => isCurrent == null ? IsRefreshCurrent(revision, activation) : isCurrent();
            var root = new GameObject("Virtual Cell", typeof(RectTransform)).GetComponent<RectTransform>();
            var cell = new Cell { Root = root, TemplateKey = templateKey };
            var accepted = false;
            Exception creationFailure = null;
            try
            {
                root.gameObject.SetActive(false);
                root.SetParent(scrollRect.content, false);
                if (!IsCurrent())
                {
                    return null;
                }

                ConfigureAxisAnchors(root);
                root.sizeDelta = IsHorizontal ? new Vector2(estimatedItemExtent, 0) : new Vector2(0, estimatedItemExtent);
                var child = Instantiate(ResolveItemTemplate(templateKey), root, false);
                if (!IsCurrent())
                {
                    return null;
                }

                var rect = (RectTransform)child.transform;
                cell.MeasurementRoot = rect;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                child.gameObject.SetActive(true);
                if (!IsCurrent())
                {
                    return null;
                }

                var element = root.gameObject.AddComponent<NestedViewElement>();
                cell.Element = element;
                element.Initialize();
                if (!IsCurrent())
                {
                    return null;
                }

                ((IChildViewElement)element).BeginParentActivation(scope, activation);
                if (!IsCurrent())
                {
                    return null;
                }

                AttachSelection(cell, child);
                AttachNativeFocus(cell, child);
                accepted = IsCurrent();
                return accepted ? cell : null;
            }
            catch (Exception error)
            {
                creationFailure = error;
                throw;
            }
            finally
            {
                // 尚未交给工作集的实例也有明确释放责任，不能漏过父级同步关闭的回调窗口。
                if (!accepted)
                {
                    try
                    {
                        ReleaseCell(cell);
                    }
                    catch (Exception cleanup)
                    {
                        if (creationFailure != null)
                        {
                            throw new AggregateException("Virtual list creation and cleanup failed.", creationFailure, cleanup);
                        }

                        throw;
                    }
                }
            }
        }

        private static void ReleaseCell(Cell cell)
        {
            var errors = new List<Exception>();
            try
            {
                View.ReleaseFocusWithin(cell.Root);
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                DetachSelection(cell);
                DetachNativeFocus(cell);
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                if (cell.Element != null)
                {
                    cell.Element.Dispose();
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            finally
            {
                if (cell.Root != null)
                {
                    Destroy(cell.Root.gameObject);
                }
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("Virtual list cell cleanup failed.", errors);
            }
        }

        private static async Task ObserveAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private sealed class Cell
        {
            // 换源或 Reset 后，即使业务键相同，也必须解除旧来源的绑定和焦点。
            public long SourceGeneration;
            public RectTransform Root;
            public RectTransform MeasurementRoot;
            public NestedViewElement Element;
            public object Key;
            public string TemplateKey;
            public VirtualListItemSelection Selection;
            public Button SelectionButton;
            public UnityEngine.Events.UnityAction SelectionHandler;
            public readonly List<VirtualListFocusInput> FocusInputs = new List<VirtualListFocusInput>();
        }
    }
}
