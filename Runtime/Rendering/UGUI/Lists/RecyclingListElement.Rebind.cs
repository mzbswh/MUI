using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.UGUI
{
    public partial class RecyclingListElement
    {
        /// <summary>准备期间保留来源订阅和所有可见条目，只在隐藏节点中准备新增内容。</summary>
        async ValueTask<IPreparedBindingTarget> IBindingRebindTarget.PrepareRebindAsync(
            IReadOnlyDictionary<string, object> values, CancellationToken cancellationToken)
        {
            RequireListAlive();
            RequireNoRebindCommit();
            var bindsItems = values.TryGetValue(nameof(Items), out var value);
            if (bindsItems && value != null && !(value is IReadOnlyObservableList<ViewModel>))
            {
                throw new InvalidOperationException("Recycling list Items binding must produce an observable list or null.");
            }

            var next = bindsItems ? (IReadOnlyObservableList<ViewModel>)value : items;
            var preparation = new PreparedRebind(this, next, bindsItems);
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
                    throw new AggregateException("Recycling list preparation and cleanup failed.", failure, cleanup);
                }

                throw;
            }
        }

        private void RequireNoRebindCommit()
        {
            if (preparedRebind != null)
            {
                throw new InvalidOperationException("Cannot change recycling list content during its binding commit.");
            }
        }

        private sealed class PreparedRebind : IPreparedBindingTarget, ICleanupResponsibilitySource
        {
            private readonly RecyclingListElement element;
            private readonly ChildViewScope scope;
            private readonly LifetimeScope lifetime;
            private Task priorChange;
            private readonly long assignment;
            private readonly long revision;
            private IReadOnlyObservableList<ViewModel> previousSource;
            private IReadOnlyObservableList<ViewModel> source;
            private readonly bool bindsItems;
            private readonly List<PreparedCell> prepared = new List<PreparedCell>();
            private readonly List<Func<bool>> cleanupConfirmations = new List<Func<bool>>();
            private List<ViewModel> models;
            private long sourceVersion;
            private int writes;
            private bool started;
            private bool committing;
            private bool disposed;

            internal PreparedRebind(RecyclingListElement element, IReadOnlyObservableList<ViewModel> source,
                bool bindsItems)
            {
                this.element = element;
                this.source = source;
                this.bindsItems = bindsItems;
                scope = element.scope;
                lifetime = element.lifetime;
                priorChange = element.pending;
                assignment = element.itemsAssignmentVersion;
                revision = element.revision;
                previousSource = element.items;
                var failureRecorded = false;
                CleanupResponsibility = new CleanupResponsibility(ReleasePreparedAsync, "RecyclingListRebindPreparation", error =>
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
                    catch (Exception) when (priorChange.IsCompleted && !token.IsCancellationRequested &&
                        !lifetime.IsEnded)
                    {
                        // 前次失败已排空，其结果仍由原等待者持有；允许本次显式换绑重试。
                    }
                }

                token.ThrowIfCancellationRequested();
                RequireCurrent();
                sourceVersion = source == null ? -1 : source.Version;
                models = element.ReadSnapshot(source);
                Validate();
                if (!bindsItems)
                {
                    return;
                }

                var count = Math.Max(models.Count, element.cells.Count);
                for (var index = 0; index < count; ++index)
                {
                    token.ThrowIfCancellationRequested();
                    Validate();
                    var created = index >= element.cells.Count;
                    var cell = created ? CreateCell() : element.RequireCell(index);
                    var entry = new PreparedCell(cell, created);
                    prepared.Add(entry);
                    var model = index < models.Count ? models[index] : null;
                    entry.Model = model;
                    entry.Binding = await ((IBindingRebindTarget)cell).PrepareRebindAsync(
                        new Dictionary<string, object> { [nameof(NestedViewElement.ViewModel)] = model }, token);
                    if (entry.Binding == null)
                    {
                        throw new InvalidOperationException("Recycling list child returned no rebind preparation.");
                    }
                }
            }

            private NestedViewElement CreateCell()
            {
                if (element.itemTemplate == null || element.content == null || element.itemTemplate.gameObject.activeSelf)
                {
                    throw new InvalidOperationException("Recycling list template or content is unavailable.");
                }

                var cell = UnityEngine.Object.Instantiate(element.itemTemplate, element.content, false);
                try
                {
                    cell.Initialize();
                    ((IChildViewElement)cell).BeginParentActivation(scope, lifetime);
                    return cell;
                }
                catch
                {
                    element.ReleaseCell(cell);
                    throw;
                }
            }

            private void RequireCurrent()
            {
                element.RequireListAlive();
                if (disposed || scope == null || lifetime == null || !scope.IsActive || lifetime.IsEnded ||
                    !ReferenceEquals(element.scope, scope) || !ReferenceEquals(element.lifetime, lifetime) ||
                    !ReferenceEquals(element.items, previousSource) || assignment != element.itemsAssignmentVersion ||
                    revision != element.revision || !ReferenceEquals(element.pending, priorChange))
                {
                    throw new OperationCanceledException("Recycling list changed during rebind preparation.");
                }
            }

            public void Validate()
            {
                RequireCurrent();
                if ((priorChange != null && !priorChange.IsCompleted) ||
                    sourceVersion != (source == null ? -1 : source.Version))
                {
                    throw new OperationCanceledException("Recycling list source changed during rebind preparation.");
                }

                foreach (var entry in prepared)
                {
                    if (entry.Cell == null || !entry.Cell.IsAlive ||
                        (element.UsesFixedSlots ? !element.fixedCellParents.TryGetValue(entry.Cell, out var parent) ||
                            entry.Cell.transform.parent != parent : entry.Cell.transform.parent != element.content))
                    {
                        throw new InvalidOperationException("Prepared recycling list cell was destroyed or moved.");
                    }

                    entry.Binding?.Validate();
                }
            }

            public void BeginCommit()
            {
                Validate();
                element.RequireNoRebindCommit();
                element.preparedRebind = this;
                started = true;
                foreach (var entry in prepared)
                {
                    entry.Binding.BeginCommit();
                }
            }

            internal void Write(IReadOnlyObservableList<ViewModel> value)
            {
                if (!started || committing || !bindsItems || ++writes != 1 || !ReferenceEquals(value, source))
                {
                    throw new InvalidOperationException("Recycling list writes differ from the prepared source.");
                }
            }

            public void Commit()
            {
                Validate();
                if (!started || !ReferenceEquals(element.preparedRebind, this) || writes != (bindsItems ? 1 : 0))
                {
                    throw new InvalidOperationException("Recycling list binding did not write the prepared source.");
                }

                committing = true;
                if (bindsItems)
                {
                    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    element.pending = completion.Task;
                    _ = ObserveAsync(completion.Task);
                    try
                    {
                        // SetItems 沿用既有订阅协议；当前准备阻止 AcceptSnapshot 再启动一套刷新。
                        element.SetItems(source);
                        if (!element.IsActivationCurrent(lifetime) || !ReferenceEquals(element.items, source) ||
                            sourceVersion != (source == null ? -1 : source.Version))
                        {
                            throw new OperationCanceledException("Recycling list source changed during commit.");
                        }

                        element.snapshot = models;
                        ++element.revision;
                        element.Error = null;
                        element.dirty = false;
                        foreach (var entry in prepared)
                        {
                            if (entry.Created)
                            {
                                element.cells.Add(entry.Cell);
                                entry.Adopted = true;
                            }

                            entry.Cell.ViewModel = entry.Model;
                            entry.Binding.Commit();
                            entry.Cell.gameObject.SetActive(entry.Model != null);
                        }

                        var readiness = new Task[prepared.Count];
                        for (var index = 0; index < prepared.Count; ++index)
                        {
                            readiness[index] = ((IChildViewElement)prepared[index].Cell).Preparation;
                        }

                        _ = CompleteCommitAsync(readiness, completion);
                    }
                    catch (Exception error)
                    {
                        completion.TrySetException(error);
                        throw;
                    }
                }

                element.preparedRebind = null;
            }

            private static async Task CompleteCommitAsync(Task[] readiness, TaskCompletionSource<bool> completion)
            {
                try
                {
                    var errors = new List<Exception>();
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
                        throw new AggregateException("Recycling list rebind cleanup failed.", errors);
                    }

                    completion.TrySetResult(true);
                }
                catch (Exception error)
                {
                    completion.TrySetException(error);
                }
            }

            public ValueTask DisposeAsync() => CleanupResponsibility.DisposeAsync();

            private async ValueTask ReleasePreparedAsync()
            {
                if (disposed)
                {
                    // 原清理任务保留失败；恢复只确认已关闭子视图及最终节点责任，不重新解绑。
                    foreach (var confirmed in cleanupConfirmations)
                    {
                        if (!confirmed())
                        {
                            throw new InvalidOperationException("Recycling list preparation cleanup dependencies are unconfirmed.");
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
                        cleanupConfirmations.Add(entry.Cell.CaptureChildViewCleanupConfirmation());
                    }

                    if (entry.Created && !entry.Adopted)
                    {
                        try
                        {
                            element.ReleaseCell(entry.Cell);
                        }
                        catch (Exception error)
                        {
                            errors.Add(error);
                            cleanupConfirmations.Add(entry.Cell.CaptureNodeCleanupConfirmation());
                        }
                    }
                }

                // 已接管节点和快照由列表持有；本准备不再保留业务模型引用。
                models = null;
                previousSource = null;
                source = null;
                priorChange = null;
                prepared.Clear();

                if (errors.Count != 0)
                {
                    throw new AggregateException("Recycling list rebind cleanup failed.", errors);
                }
            }
        }

        private sealed class PreparedCell
        {
            internal readonly NestedViewElement Cell;
            internal readonly bool Created;
            internal ViewModel Model;
            internal IPreparedBindingTarget Binding;
            internal bool Adopted;

            internal PreparedCell(NestedViewElement cell, bool created)
            {
                Cell = cell;
                Created = created;
            }
        }
    }
}
