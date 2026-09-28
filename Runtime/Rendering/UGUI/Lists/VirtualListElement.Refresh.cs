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
            if (!initialized || scope == null || !scope.IsActive || (Error != null && !recover))
            {
                return;
            }

            if (!running && !dirty && first >= 0 && !recover)
            {
                try
                {
                    Layout.GetRange(snapshot.Count, scrollRect.content.anchoredPosition.y, scrollRect.viewport.rect.height, out var start, out var end);
                    if (start == first && end == last)
                    {
                        return;
                    }
                }
                catch (Exception failure)
                {
                    if (lifetime.Mode == LifetimeMode.Synchronous)
                    {
                        BeginSynchronousRefresh();
                        FailSynchronousRefresh(failure);
                    }
                    else
                    {
                        synchronousRefresh = false;
                        pending = Task.FromException(failure);
                    }

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

            if (lifetime.Mode == LifetimeMode.Synchronous)
            {
                if (IsExecutingItem())
                {
                    // 保留 dirty，下一次 LateUpdate 在命令返回后直接同步刷新。
                    return;
                }

                RefreshSynchronous();
                return;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            synchronousRefresh = false;
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

        private bool IsRefreshOperationCurrent(Lifetime activation, Task refreshTask) =>
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
            foreach (var batch in RefreshCells())
            {
                if (batch.Length == 1)
                {
                    await batch[0].Completion;
                }
                else
                {
                    var preparation = new Task[batch.Length];
                    for (var i = 0; i < batch.Length; ++i)
                    {
                        preparation[i] = batch[i].Completion;
                    }

                    await Task.WhenAll(preparation);
                }
            }
        }

        // 共用视口协调与单元所有权；每次产出是一组嵌套绑定的完成边界。
        // 异步驱动等待边界，同步驱动只推进已直接执行完毕的操作，不读取任务结果。
        private IEnumerable<CellPreparation[]> RefreshCells()
        {
            var revision = sourceRevision;
            var activation = lifetime;
            Layout.GetRange(snapshot.Count, scrollRect.content.anchoredPosition.y, scrollRect.viewport.rect.height, out var start, out var end);
            if (start == first && end == last)
            {
                yield break;
            }

            if (end - start > maxCells)
            {
                throw new InvalidOperationException("Viewport exceeds the configured virtual list cell capacity.");
            }

            var desired = snapshot.GetRange(start, end - start);
            var desiredTemplates = new Dictionary<object, string>();
            foreach (var item in desired)
            {
                desiredTemplates.Add(item.Key, item.TemplateKey);
            }

            if (!IsRefreshCurrent(revision, activation))
            {
                InvalidateAbandonedRefreshRange(activation);
                yield break;
            }

            var draining = new List<CellPreparation>();
            foreach (var cell in cells.ToArray())
            {
                if (cell.Key == null)
                {
                    continue;
                }

                var retained = desiredTemplates.TryGetValue(cell.Key, out var templateKey) &&
                    string.Equals(templateKey, cell.TemplateKey, StringComparison.Ordinal);
                if (!IsRefreshCurrent(revision, activation))
                {
                    break;
                }

                if (!retained)
                {
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

                    draining.Add(new CellPreparation(cell.Element, activation.Mode));
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

            var preparedCell = new CellPreparation[1];
            for (var i = 0; i < desired.Count; i++)
            {
                var item = desired[i];
                var cell = FindReusableCell(item, revision, activation);
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    yield break;
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
                        yield break;
                    }

                    cell = CreateCell(item.TemplateKey, revision, activation);
                    if (cell == null)
                    {
                        InvalidateAbandonedRefreshRange(activation);
                        yield break;
                    }

                    cells.Add(cell);
                }

                cell.Key = item.Key;
                var column = (start + i) % columns;
                cell.Root.anchorMin = new Vector2(column / (float)columns, 1);
                cell.Root.anchorMax = new Vector2((column + 1) / (float)columns, 1);
                cell.Root.anchoredPosition = new Vector2(0, -Layout.OffsetForIndex(start + i));
                cell.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, EffectiveHeight(item));
                // 原生尺寸回调可以同步换来源或关闭父级，旧快照不得继续启动业务绑定。
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    yield break;
                }

                cell.Element.ViewModel = item.ViewModel;
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    yield break;
                }

                preparedCell[0] = new CellPreparation(cell.Element, activation.Mode);
                yield return preparedCell;
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    yield break;
                }

                if (cell.Selection != null)
                {
                    var selection = selectionRevision;
                    var selected = Equals(cell.Key, selectedKey);
                    if (!IsRefreshCurrent(revision, activation))
                    {
                        InvalidateAbandonedRefreshRange(activation);
                        yield break;
                    }

                    if (selection == selectionRevision)
                    {
                        cell.Selection.SetSelected(selected);
                    }
                }

                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    yield break;
                }

                cell.Root.gameObject.SetActive(true);
                if (!IsRefreshCurrent(revision, activation))
                {
                    InvalidateAbandonedRefreshRange(activation);
                    yield break;
                }
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

        private Cell FindReusableCell(VirtualListItem item, long revision, Lifetime activation)
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

        private bool IsRefreshCurrent(long revision, Lifetime activation) =>
            IsAlive && scope != null && scope.IsActive && ReferenceEquals(lifetime, activation) &&
            !activation.IsEnded && revision == sourceRevision && !dirty && Error == null;

        private void InvalidateAbandonedRefreshRange(Lifetime activation)
        {
            if (IsAlive && ReferenceEquals(lifetime, activation))
            {
                first = last = -1;
            }
        }

        private Cell CreateCell(string templateKey, long revision, Lifetime activation)
        {
            var root = new GameObject("Virtual Cell", typeof(RectTransform)).GetComponent<RectTransform>();
            var cell = new Cell { Root = root, TemplateKey = templateKey };
            var accepted = false;
            Exception creationFailure = null;
            try
            {
                root.gameObject.SetActive(false);
                root.SetParent(scrollRect.content, false);
                if (!IsRefreshCurrent(revision, activation))
                {
                    return null;
                }

                root.anchorMin = new Vector2(0, 1);
                root.anchorMax = Vector2.one;
                root.pivot = new Vector2(0.5f, 1);
                root.sizeDelta = new Vector2(0, rowHeight);
                var child = Instantiate(ResolveItemTemplate(templateKey), root, false);
                if (!IsRefreshCurrent(revision, activation))
                {
                    return null;
                }

                var rect = (RectTransform)child.transform;
                cell.MeasurementRoot = rect;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                child.gameObject.SetActive(true);
                if (!IsRefreshCurrent(revision, activation))
                {
                    return null;
                }

                var element = root.gameObject.AddComponent<NestedViewElement>();
                cell.Element = element;
                element.Initialize();
                if (!IsRefreshCurrent(revision, activation))
                {
                    return null;
                }

                ((IChildViewElement)element).BeginParentActivation(scope, activation);
                if (!IsRefreshCurrent(revision, activation))
                {
                    return null;
                }

                AttachSelection(cell, child);
                accepted = IsRefreshCurrent(revision, activation);
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
                DetachSelection(cell);
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

        private readonly struct CellPreparation
        {
            public CellPreparation(NestedViewElement element, LifetimeMode mode)
            {
                // 异步模式立即捕获本次操作信号，后续回调不能把它替换成另一轮任务。
                Completion = mode == LifetimeMode.Synchronous ? null : ((IChildViewElement)element).Preparation;
            }

            public Task Completion
            {
                get;
            }
        }

        private sealed class Cell
        {
            public RectTransform Root;
            public RectTransform MeasurementRoot;
            public NestedViewElement Element;
            public object Key;
            public string TemplateKey;
            public VirtualListItemSelection Selection;
            public Button SelectionButton;
            public UnityEngine.Events.UnityAction SelectionHandler;
        }
    }
}
