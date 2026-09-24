using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        /// <summary>
        /// 替换前校验完整目录。仅当所选定义仍为同一个
        /// 不可变对象且可用时保留选择；否则优先选择
        /// 指定的可用键，再按输入顺序选择第一个可用项。
        /// 更新被接受后，取消只结束当前调用者对回退选择的等待。
        /// </summary>
        public ValueTask<TabSelectionResult> UpdateDefinitionsAsync(IEnumerable<TabContentDefinition> replacement,
            string preferredKey = null,
            CancellationToken cancellationToken = default)
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

            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Result(TabSelectionStatus.WaitCancelled);
            }

            var catalog = new Dictionary<string, TabContentDefinition>(StringComparer.Ordinal);
            var items = new List<TabItemState>();
            string fallback = null;
            var keep = false;
            var accepted = false;
            changing = true;
            try
            {
                foreach (var definition in replacement)
                {
                    if (definition == null)
                    {
                        throw new ArgumentException("Null Tab definition.", nameof(replacement));
                    }

                    RequireDefinitionMode(definition);
                    catalog.Add(definition.Key, definition);
                    var enabled = definition.IsEnabled();
                    items.Add(new TabItemState(definition.Key, definition.Label, enabled));
                    if (enabled && (fallback == null || definition.Key == preferredKey))
                    {
                        fallback = definition.Key;
                    }

                    if (enabled &&
                        definition.Key == ViewModel.Snapshot.SelectedTab &&
                        definitions.TryGetValue(definition.Key, out var previous) &&
                        ReferenceEquals(previous, definition))
                    {
                        keep = true;
                    }
                }

                if (inactive || !scope.IsActive)
                {
                    return Result(TabSelectionStatus.ParentInactive);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return Result(TabSelectionStatus.WaitCancelled);
                }

                CancelLeaveRequests(TabSelectionStatus.Superseded);
                definitions.Clear();
                foreach (var pair in catalog)
                {
                    definitions.Add(pair.Key, pair.Value);
                }

                ViewModel.ReplaceItems(items);
                accepted = true;
                InvalidateRetainedContent(catalog, items);
                ScheduleCacheInvalidation();
                if (!keep)
                {
                    if (operation != null && !operation.Completion.Task.IsCompleted)
                    {
                        operation.Completion.TrySetResult(new TabSelectionResult(TabSelectionStatus.Superseded));
                        Cancel(operation);
                    }

                    operation = null;
                    ClearContent();

                    if (inactive || !scope.IsActive)
                    {
                        return Result(TabSelectionStatus.ParentInactive);
                    }

                    Publish(new TabSnapshot(null, null, TabPhase.Empty, null, ++version));
                }
            }
            catch (Exception error)
            {
                if (accepted && scope.IsActive && !inactive)
                {
                    Publish(new TabSnapshot(null, null, TabPhase.Error, error, ++version));
                }

                return Result(TabSelectionStatus.Failed, error);
            }
            finally
            {
                if (accepted)
                {
                    ViewModel.NotifyItemsChanged();
                }

                changing = false;
            }

            ValueTask<TabSelectionResult> selection;
            if (keep)
            {
                var snapshot = ViewModel.Snapshot;
                if (snapshot.Phase == TabPhase.Loading && operation != null)
                {
                    selection = new ValueTask<TabSelectionResult>(WaitAsync(operation.Completion.Task, cancellationToken));
                }
                else if (snapshot.Phase == TabPhase.Ready)
                {
                    selection = Result(TabSelectionStatus.Ready);
                }
                else if (snapshot.Phase == TabPhase.Error)
                {
                    selection = Result(TabSelectionStatus.Failed, snapshot.Error);
                }
                else
                {
                    selection = Result(TabSelectionStatus.Empty);
                }
            }
            else if (fallback == null)
            {
                selection = Result(TabSelectionStatus.Empty);
            }
            else
            {
                selection = new ValueTask<TabSelectionResult>(WaitAsync(SelectAsync(fallback,
                    forceReload: true).AsTask(),
                    cancellationToken));
            }

            return selection;
        }

        /// <summary>重新求值可用性，并按目录顺序替换已禁用的选择。</summary>
        public ValueTask<TabSelectionResult> ReconcileAvailabilityAsync(string preferredKey = null,
            CancellationToken cancellationToken = default)
        {
            RequireAsyncAllowed();
            var ordered = new List<TabContentDefinition>();
            foreach (var item in ViewModel.Items)
            {
                if (definitions.TryGetValue(item.Key, out var definition))
                {
                    ordered.Add(definition);
                }
            }

            return UpdateDefinitionsAsync(ordered, preferredKey, cancellationToken);
        }
    }
}
