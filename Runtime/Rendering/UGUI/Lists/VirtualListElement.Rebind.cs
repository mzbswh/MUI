using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        /// <summary>仅准备候选视口，不为换绑物化完整数据；旧来源、画面和滚动位置仍有效。</summary>
        async ValueTask<IPreparedBindingTarget> IBindingRebindTarget.PrepareRebindAsync(
            IReadOnlyDictionary<string, object> values, CancellationToken cancellationToken)
        {
            RequireListAlive();
            if (preparedRebind != null)
            {
                throw new InvalidOperationException("Virtual list already has a binding commit.");
            }

            var preparation = new PreparedRebind(this, values);
            try
            {
                await preparation.PrepareAsync(cancellationToken);
                preparation.Validate();
                return preparation;
            }
            catch (Exception failure)
            {
                try
                {
                    await preparation.DisposeAsync();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Virtual list preparation and cleanup failed.", failure, cleanup);
                }

                throw;
            }
        }

        private sealed class PreparedRebind : IPreparedBindingTarget, ICleanupResponsibilitySource
        {
            private readonly VirtualListElement element;
            private readonly ChildViewScope scope;
            private readonly LifetimeScope lifetime;
            private Task priorChange;
            private readonly long assignment;
            private readonly long revision;
            private IReadOnlyObservableList<VirtualListItem> previousSource;
            private IReadOnlyObservableList<VirtualListItem> source;
            private readonly int previousColumns;
            private readonly int columns;
            private readonly float viewportExtent;
            private readonly float crossExtent;
            private readonly float previousOffset;
            private readonly bool bindsItems;
            private readonly bool bindsColumns;
            private readonly bool bindsSelection;
            private object selection;
            private readonly List<PreparedCell> prepared = new List<PreparedCell>();
            private readonly List<Func<bool>> cleanupConfirmations = new List<Func<bool>>();
            private readonly List<Task> retirements = new List<Task>();
            private List<VirtualListItem> items;
            private VirtualRowIndex offsets;
            private Dictionary<VirtualListItem, float> measurements;
            private float candidateOffset;
            private long sourceVersion;
            private int start;
            private int end;
            private int itemWrites;
            private int columnWrites;
            private int selectionWrites;
            private bool started;
            private bool committing;
            private bool disposed;

            internal PreparedRebind(VirtualListElement element, IReadOnlyDictionary<string, object> values)
            {
                this.element = element;
                scope = element.scope;
                lifetime = element.lifetime;
                priorChange = element.pending;
                assignment = element.itemsAssignmentVersion;
                revision = element.sourceRevision;
                previousSource = element.items;
                previousColumns = element.columns;
                viewportExtent = element.ViewportExtent;
                crossExtent = element.ItemCrossExtent;
                previousOffset = element.ScrollOffset;
                bindsItems = values.TryGetValue(nameof(Items), out var sourceValue);
                bindsColumns = values.TryGetValue(nameof(Columns), out var columnsValue);
                bindsSelection = values.TryGetValue(nameof(SelectedKey), out selection);
                if (bindsItems && sourceValue != null && !(sourceValue is IReadOnlyObservableList<VirtualListItem>))
                {
                    throw new InvalidOperationException("Virtual Items binding must produce an observable list or null.");
                }

                if (bindsColumns && (!(columnsValue is int) || element.automaticColumns))
                {
                    throw new InvalidOperationException("Virtual Columns binding requires an integer and manual columns.");
                }

                source = bindsItems ? (IReadOnlyObservableList<VirtualListItem>)sourceValue : previousSource;
                columns = bindsColumns ? (int)columnsValue : previousColumns;
                if (columns < 1 || columns > element.maxCells || (element.IsHorizontal && columns != 1))
                {
                    throw new InvalidOperationException("Prepared virtual list columns are invalid.");
                }

                var failureRecorded = false;
                CleanupResponsibility = new CleanupResponsibility(ReleasePreparedAsync, "VirtualListRebindPreparation", error =>
                {
                    if (error != null && lifetime != null && !failureRecorded)
                    {
                        failureRecorded = true;
                        lifetime.RecordCleanupFailure(error, CleanupResponsibility);
                    }
                }, true, Thread.CurrentThread.ManagedThreadId);
            }

            public CleanupResponsibility CleanupResponsibility
            {
                get;
            }

            internal async ValueTask PrepareAsync(CancellationToken token)
            {
                RequireCurrent();
                if (priorChange != null)
                {
                    try
                    {
                        await View.WaitForPreparationAsync(priorChange, token, lifetime.Token);
                    }
                    catch (Exception) when (priorChange.IsCompleted && !token.IsCancellationRequested && !lifetime.IsEnded)
                    {
                        // 已排空失败归原观察者；显式换绑可以提供新的有效来源。
                    }
                }

                token.ThrowIfCancellationRequested();
                RequireCurrent();
                sourceVersion = source == null ? -1 : source.Version;
                items = ReadSnapshot(source);
                measurements = columns == previousColumns && ReferenceEquals(source, previousSource)
                    ? PrepareMeasurements(items, element.measuredItems, false)
                    : new Dictionary<VirtualListItem, float>();
                offsets = element.PrepareSnapshot(items, measurements, columns);
                var layout = element.CreateLayout(columns, element.overscan, offsets);
                candidateOffset = 0;
                if (ReferenceEquals(source, previousSource) && items.Count != 0)
                {
                    var anchor = element.FirstVisibleIndex;
                    var within = anchor < 0 ? 0 : previousOffset - element.Layout.OffsetForIndex(anchor);
                    candidateOffset = element.ShouldFollowEnd()
                        ? Math.Max(0, layout.ContentExtent(items.Count) - viewportExtent)
                        : anchor < 0 ? 0 : layout.OffsetForIndex(anchor) + Math.Min(within, layout.RowExtentForIndex(anchor));
                }

                layout.GetRange(items.Count, candidateOffset, viewportExtent, out start, out end);
                if (end - start > element.maxCells)
                {
                    throw new InvalidOperationException("Prepared virtual viewport exceeds its cell capacity.");
                }

                if (bindsSelection && selection != null && !items.Exists(item => Equals(item.Key, selection)))
                {
                    throw new InvalidOperationException("Prepared selection is not in the candidate source.");
                }

                Validate();
                for (var index = start; index < end; ++index)
                {
                    token.ThrowIfCancellationRequested();
                    Validate();
                    var item = items[index];
                    // 隐藏候选有独立节点，旧在途命令或同键换模板不会被复用成新内容。
                    var cell = element.CreateCell(item.TemplateKey, revision, lifetime, () =>
                    {
                        RequireCurrent();
                        return true;
                    });
                    if (cell == null)
                    {
                        throw new OperationCanceledException("Virtual candidate was invalidated.");
                    }

                    var entry = new PreparedCell(cell, item, index);
                    prepared.Add(entry);
                    entry.Binding = await ((IBindingRebindTarget)cell.Element).PrepareRebindAsync(
                        new Dictionary<string, object> { [nameof(NestedViewElement.ViewModel)] = item.ViewModel }, token);
                    if (entry.Binding == null)
                    {
                        throw new InvalidOperationException("Virtual child returned no binding preparation.");
                    }
                }
            }

            private void RequireCurrent()
            {
                element.RequireListAlive();
                if (disposed || scope == null || lifetime == null || !scope.IsActive || lifetime.IsEnded ||
                    !ReferenceEquals(element.scope, scope) || !ReferenceEquals(element.lifetime, lifetime) ||
                    !ReferenceEquals(element.items, previousSource) || assignment != element.itemsAssignmentVersion ||
                    revision != element.sourceRevision || !ReferenceEquals(element.pending, priorChange) ||
                    element.columns != previousColumns || element.ViewportExtent != viewportExtent ||
                    element.ItemCrossExtent != crossExtent || element.ScrollOffset != previousOffset)
                {
                    throw new OperationCanceledException("Virtual list changed during rebind preparation.");
                }
            }

            public void Validate()
            {
                RequireCurrent();
                if ((priorChange != null && !priorChange.IsCompleted) ||
                    sourceVersion != (source == null ? -1 : source.Version))
                {
                    throw new OperationCanceledException("Virtual source changed during rebind preparation.");
                }

                foreach (var entry in prepared)
                {
                    if (entry.Cell.Root == null || entry.Cell.Root.parent != element.scrollRect.content)
                    {
                        throw new InvalidOperationException("Prepared virtual cell was destroyed or moved.");
                    }

                    entry.Binding?.Validate();
                }
            }

            public void BeginCommit()
            {
                Validate();
                if (element.preparedRebind != null)
                {
                    throw new InvalidOperationException("Virtual list already has a binding commit.");
                }

                element.preparedRebind = this;
                started = true;
                foreach (var entry in prepared)
                {
                    entry.Binding.BeginCommit();
                }
            }

            internal void WriteItems(IReadOnlyObservableList<VirtualListItem> value)
            {
                RequireWrite(bindsItems, ++itemWrites);
                if (!ReferenceEquals(value, source))
                {
                    throw new InvalidOperationException("Virtual Items write differs from prepared source.");
                }
            }

            internal void WriteColumns(int value)
            {
                RequireWrite(bindsColumns, ++columnWrites);
                if (value != columns)
                {
                    throw new InvalidOperationException("Virtual Columns write differs from prepared value.");
                }
            }

            internal void WriteSelection(object value)
            {
                RequireWrite(bindsSelection, ++selectionWrites);
                if (!Equals(value, selection))
                {
                    throw new InvalidOperationException("Virtual selection differs from prepared value.");
                }
            }

            private void RequireWrite(bool declared, int writes)
            {
                if (!started || committing || !declared || writes != 1)
                {
                    throw new InvalidOperationException("Cannot change virtual content during its binding commit.");
                }
            }

            public void Commit()
            {
                Validate();
                if (!started || !ReferenceEquals(element.preparedRebind, this) ||
                    itemWrites != (bindsItems ? 1 : 0) || columnWrites != (bindsColumns ? 1 : 0) ||
                    selectionWrites != (bindsSelection ? 1 : 0))
                {
                    throw new InvalidOperationException("Virtual binding did not write its prepared values.");
                }

                committing = true;
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                element.pending = completion.Task;
                element.running = true;
                _ = ObserveAsync(completion.Task);
                try
                {
                    element.columns = columns;
                    element.SetItems(source);
                    RequireCommittedSource();

                    var oldCells = element.cells.ToArray();
                    foreach (var cell in oldCells)
                    {
                        View.ReleaseFocusWithin(cell.Root);
                        cell.Root.gameObject.SetActive(false);
                        cell.Element.ViewModel = null;
                        retirements.Add(((IChildViewElement)cell.Element).Preparation);
                    }

                    // 所有旧节点仍由列表持有，完成退役后才物理释放。
                    element.ApplyPreparedSnapshot(items, offsets, sourceVersion, measurements, preservePosition: false);
                    RequireCommittedSource();
                    if (columns != previousColumns)
                    {
                        element.measurementCrossExtent = -1;
                    }

                    element.SetReadingOffset(candidateOffset, preserveVelocity: false);
                    element.ClearItemFailures();
                    foreach (var entry in prepared)
                    {
                        RequireCommittedSource();
                        element.cells.Add(entry.Cell);
                        entry.Adopted = true;
                        entry.Cell.Key = entry.Item.Key;
                        entry.Cell.SourceGeneration = element.revealSourceRevision;
                        element.PositionCell(entry.Cell.Root, entry.Index, element.EffectiveExtent(entry.Item));
                        entry.Cell.Element.ViewModel = entry.Item.ViewModel;
                        entry.Binding.Commit();
                        entry.Cell.Root.gameObject.SetActive(true);
                    }

                    RequireCommittedSource();
                    element.SetSelection(bindsSelection ? selection : element.selectedKey);
                    RequireCommittedSource();

                    element.first = start;
                    element.last = end;
                    element.dirty = false;
                    element.preparedRebind = null;
                    var readiness = new Task[prepared.Count];
                    for (var index = 0; index < prepared.Count; ++index)
                    {
                        readiness[index] = ((IChildViewElement)prepared[index].Cell.Element).Preparation;
                    }

                    _ = CompleteCommitAsync(oldCells, readiness, retirements.ToArray(), items.Count, completion);
                }
                catch (Exception error)
                {
                    element.running = false;
                    completion.TrySetException(error);
                    throw;
                }
            }

            private void RequireCommittedSource()
            {
                if (!element.IsAlive || element.scope != scope || lifetime.IsEnded ||
                    !ReferenceEquals(element.items, source) || element.columns != columns ||
                    sourceVersion != (source == null ? -1 : source.Version))
                {
                    throw new OperationCanceledException("Virtual source changed during commit.");
                }
            }

            private async Task CompleteCommitAsync(Cell[] oldCells, Task[] readiness, Task[] retirementTasks,
                int itemCount, TaskCompletionSource<bool> completion)
            {
                try
                {
                    var errors = new List<Exception>();
                    foreach (var task in retirementTasks)
                    {
                        try
                        {
                            await task;
                        }
                        catch (Exception error)
                        {
                            errors.Add(error);
                        }
                    }

                    // 清理未成功的旧节点继续由列表和父 Scope 保留，不提前销毁其展示资源。
                    if (errors.Count == 0)
                    {
                        foreach (var cell in oldCells)
                        {
                            try
                            {
                                // 父级关闭可能已接管节点清理，迟到收尾不能再次释放。
                                if (element.cells.Remove(cell))
                                {
                                    ReleaseCell(cell);
                                }
                            }
                            catch (Exception error)
                            {
                                errors.Add(error);
                            }
                        }
                    }

                    foreach (var task in readiness)
                    {
                        try
                        {
                            await task;
                        }
                        catch (Exception error)
                        {
                            errors.Add(error);
                        }
                    }

                    if (errors.Count != 0)
                    {
                        throw new AggregateException("Virtual list rebind or retirement failed.", errors);
                    }

                    if (element.IsAlive && ReferenceEquals(element.pending, completion.Task) && !lifetime.IsEnded)
                    {
                        element.SetStatus(itemCount == 0 ? VirtualListStatus.Empty : VirtualListStatus.Ready);
                    }

                    completion.TrySetResult(true);
                }
                catch (Exception error)
                {
                    completion.TrySetException(error);
                }
                finally
                {
                    if (ReferenceEquals(element.pending, completion.Task))
                    {
                        element.running = false;
                    }
                }
            }

            public ValueTask DisposeAsync() => CleanupResponsibility.DisposeAsync();

            private async ValueTask ReleasePreparedAsync()
            {
                if (disposed)
                {
                    // 不重放候选回调；叶责任与节点归还后才确认本容器，不改写首次失败结果。
                    foreach (var confirmed in cleanupConfirmations)
                    {
                        if (!confirmed())
                        {
                            throw new InvalidOperationException("Virtual list preparation cleanup dependencies are unconfirmed.");
                        }
                    }
                    cleanupConfirmations.Clear();
                    return;
                }

                disposed = true;
                if (ReferenceEquals(element.preparedRebind, this))
                {
                    element.preparedRebind = null;
                }

                var errors = new List<Exception>();
                for (var index = prepared.Count - 1; index >= 0; --index)
                {
                    var entry = prepared[index];
                    try
                    {
                        if (entry.Binding != null)
                        {
                            await entry.Binding.DisposeAsync();
                        }
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                        cleanupConfirmations.Add(entry.Cell.Element.CaptureChildViewCleanupConfirmation());
                    }

                    if (!entry.Adopted)
                    {
                        try
                        {
                            ReleaseCell(entry.Cell);
                        }
                        catch (Exception error)
                        {
                            errors.Add(error);
                            cleanupConfirmations.Add(entry.Cell.Element.CaptureNodeCleanupConfirmation());
                        }
                    }
                }

                items = null;
                previousSource = null;
                source = null;
                priorChange = null;
                selection = null;
                measurements = null;
                offsets = null;
                prepared.Clear();
                retirements.Clear();
                if (errors.Count != 0)
                {
                    throw new AggregateException("Virtual rebind cleanup failed.", errors);
                }
            }
        }

        private sealed class PreparedCell
        {
            internal readonly Cell Cell;
            internal readonly VirtualListItem Item;
            internal readonly int Index;
            internal IPreparedBindingTarget Binding;
            internal bool Adopted;

            internal PreparedCell(Cell cell, VirtualListItem item, int index)
            {
                Cell = cell;
                Item = item;
                Index = index;
            }
        }
    }
}
