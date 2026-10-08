using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        // 仅保存一份已提交内容的身份；句柄与资源仍由原 Slot/Scope 持有。
        private RetainedContent retained;
        private TabContentDefinition displayedDefinition;

        private bool IsRetainedCallback => retained != null && retained.Handle.IsExecuting;

        private string RetainedDisplayedKey => pendingDisplay == TabPendingDisplay.KeepPrevious && retained != null && retained.Handle.State == ChildViewState.Retained
                    ? retained.Definition.Key : null;

        private void PreparePreviousContent()
        {
            if (pendingDisplay != TabPendingDisplay.KeepPrevious && failureDisplay != TabFailureDisplay.RestorePrevious && cacheOptions.Capacity == 0)
            {
                ClearContent();
                return;
            }

            var current = slot.Current;
            if (current != null)
            {
                if (displayedDefinition == null)
                {
                    throw new InvalidOperationException("Committed Tab content has no definition.");
                }

                retained = new RetainedContent { Handle = current, Definition = displayedDefinition };
                // 先登记身份，取消回调重入时仍能识别旧内容的调用栈。
                current.RetainAndDeactivate();
                if (pendingDisplay != TabPendingDisplay.KeepPrevious)
                {
                    current.EndRetainedDisplay();
                }
            }

            scope.RequireActive();
        }

        private void ClearContent()
        {

            var clear = slot.ClearAsync();
            if (!clear.IsCompleted)
            {
                throw new InvalidOperationException("Logical content clearing must be synchronous.");
            }

            var result = clear.GetAwaiter().GetResult();
            if (result.Status == ChildViewChangeStatus.Failed)
            {
                throw result.Error ?? new InvalidOperationException("Failed to clear old Tab content.");
            }

            if (result.Status == ChildViewChangeStatus.ParentInactive)
            {
                throw new OperationCanceledException("Parent is inactive.");
            }

            retained = null;
            displayedDefinition = null;
        }

        private Exception ClearFailedContent(Exception failure)
        {
            try
            {
                ClearContent();
                return failure;
            }
            catch (Exception cleanup)
            {
                return failure == null ? cleanup : new AggregateException("Tab change and content clearing failed.", failure, cleanup);
            }
        }

        private async ValueTask<ChildViewHandle> PrepareSelectionAsync(
                    Operation target,
                    TabContentDefinition definition,
                    ChildViewScope owner,
                    CancellationToken token,
                    bool forceReload)
        {
            var previous = preparing.Value;
            var frame = new PreparationFrame();
            preparing.Value = frame;
            ChildViewHandle candidate = null;
            try
            {
                RequireSelectionCurrent(target, definition);
                token.ThrowIfCancellationRequested();
                var saved = retained;
                ChildViewHandle prepared;
                if (!forceReload && saved != null && ReferenceEquals(saved.Definition, definition) &&
                    saved.Handle.State == ChildViewState.Retained)
                {
                    // 恢复会结束显示保留并排空旧任务；准备完成前不冒充正在显示旧内容。
                    Publish(new TabSnapshot(target.Key, null, TabPhase.Loading, null, target.Version));
                    prepared = await owner.PrepareReactivationAsync(saved.Handle, token);
                }
                else
                {
                    prepared = await TakeCachedAsync(definition, forceReload, token);
                    if (prepared == null)
                    {
                        RequireSelectionCurrent(target, definition);
                        token.ThrowIfCancellationRequested();
                        prepared = await definition.Prepare(owner, token);
                    }
                }

                if (prepared == null || !prepared.BelongsTo(owner) || prepared.State != ChildViewState.Prepared)
                {
                    throw new InvalidOperationException("Tab factory must return a Prepared handle from its supplied scope.");
                }

                // 只有确认属于本次准备的候选才承担回滚，不能关闭外部 Scope 或已活动的实例。
                candidate = prepared;
                TrackContent(candidate, definition);
                RequireSelectionCurrent(target, definition);
                token.ThrowIfCancellationRequested();
                return candidate;
            }
            catch (Exception failure)
            {
                if (candidate != null)
                {
                    try
                    {
                        await candidate.BeginClose();
                    }
                    catch (Exception cleanup)
                    {
                        throw new AggregateException("Tab preparation validation and cleanup failed.", failure, cleanup);
                    }
                }

                throw;
            }
            finally
            {
                frame.Active = false;
                preparing.Value = previous;
            }
        }

        private void RequireSelectionCurrent(Operation target, TabContentDefinition definition)
        {
            scope.RequireThread();
            if (!IsCurrent(target) || !definitions.TryGetValue(target.Key, out var current) ||
                !ReferenceEquals(current, definition))
            {
                throw new OperationCanceledException("Tab selection or definition is no longer current.");
            }

            var enabled = definition.IsEnabled();
            // 可用性谓词也是项目回调，可能同步取消父级，不能沿用调用前的资格。
            if (!IsCurrent(target) || !definitions.TryGetValue(target.Key, out current) ||
                !ReferenceEquals(current, definition))
            {
                throw new OperationCanceledException("Tab selection changed while evaluating availability.");
            }

            if (!enabled)
            {
                throw new InvalidOperationException("Selected Tab became disabled during preparation.");
            }
        }

        private void InvalidateRetainedContent(Dictionary<string, TabContentDefinition> catalog, List<TabItemState> items)
        {
            if (retained == null)
            {
                return;
            }

            var saved = retained;
            if (catalog.TryGetValue(saved.Definition.Key, out var definition) &&
                ReferenceEquals(definition, saved.Definition) &&
                items.Exists(item => item.Key == definition.Key && item.Enabled))
            {
                return;
            }

            // 目录更新不取消仍有效的新目标，只关闭已经失去定义或可用性的旧内容。
            retained = null;
            displayedDefinition = null;
            scope.Observe(saved.Handle.BeginClose());
            var snapshot = ViewModel.Snapshot;
            if (!inactive && scope.IsActive && snapshot.Phase == TabPhase.Loading && snapshot.DisplayedTab != null)
            {
                Publish(new TabSnapshot(snapshot.SelectedTab, null, snapshot.Phase, snapshot.Error,
                    snapshot.RequestVersion, snapshot.LoadingIndicatorVisible));
            }
        }

        private sealed class RetainedContent
        {
            public ChildViewHandle Handle;
            public TabContentDefinition Definition;
        }
    }
}
