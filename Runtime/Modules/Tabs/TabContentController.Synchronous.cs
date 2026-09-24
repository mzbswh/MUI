using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using MUI.ChildViews;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        private Exception synchronousDisposalFailure;
        private bool synchronousDisposalStarted;
        private bool synchronousDisposalCompleted;

        /// <summary>由父 Scope 决定整个控制器的执行模式。</summary>
        public LifetimeMode Mode => scope.Mode;

        private bool IsChanging => changing || publishing || cancellingLeaves || IsInGuard ||
            IsRetainedCallback || IsCacheCallback || slot.IsExecuting ||
            (preparing.Value != null && preparing.Value.Active);

        /// <summary>只读预检当前内容、缓存与控制器是否均可在当前调用栈同步释放。</summary>
        public bool CanDisposeSynchronously
        {
            get
            {
                scope.RequireThread();
                if (Mode != LifetimeMode.Synchronous)
                {
                    return false;
                }

                if (synchronousDisposalStarted)
                {
                    return synchronousDisposalCompleted;
                }

                if (IsChanging || !work.CanDisposeSynchronously ||
                    !slot.CanDisposeSynchronously)
                {
                    return false;
                }

                foreach (var saved in cachedContents)
                {
                    if (!saved.Handle.CanCloseSynchronously)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private void RequireDefinitionMode(TabContentDefinition definition)
        {
            if (definition.SupportsSynchronousLifecycle != (Mode == LifetimeMode.Synchronous))
            {
                throw new ArgumentException("Tab definition lifecycle must match its controller mode.");
            }
        }

        private void RequireAsyncAllowed()
        {
            scope.RequireThread();
            if (Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("Use synchronous Tab operations for a synchronous parent scope.");
            }
        }

        private void RequireSynchronousMode()
        {
            scope.RequireThread();
            if (Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("This operation requires a synchronous parent scope.");
            }
        }

        /// <summary>
        /// 同步检查守卫、准备候选、提交并回收旧内容。准备失败可保留原实例；
        /// 返回 Ready 且 Error 非空表示新内容已显示，但旧资源清理失败。
        /// </summary>
        public TabSelectionResult Select(string key, bool forceReload = false)
        {
            RequireSynchronousMode();
            if (inactive || !scope.IsActive)
            {
                return new TabSelectionResult(TabSelectionStatus.ParentInactive);
            }

            if (IsChanging)
            {
                return new TabSelectionResult(TabSelectionStatus.Rejected, TabRejection.Reentrant);
            }

            if (key == null || !definitions.TryGetValue(key, out var definition))
            {
                return new TabSelectionResult(TabSelectionStatus.Rejected, TabRejection.UnknownTab);
            }

            changing = true;
            try
            {
                return scope.RunSynchronous(() => work.Run(_ => SelectSynchronousCore(definition, forceReload)));
            }
            catch (Exception error)
            {
                return new TabSelectionResult(TabSelectionStatus.Failed, error: error);
            }
            finally
            {
                changing = false;
            }
        }

        /// <summary>同步重新创建当前选择，仍经过可用性和离开守卫。</summary>
        public TabSelectionResult Retry() => Select(ViewModel.Snapshot.SelectedTab, forceReload: true);

        private TabSelectionResult SelectSynchronousCore(TabContentDefinition definition, bool forceReload)
        {
            slot.RequireSynchronousIdle();
            if (cacheCleanupErrors.Count != 0)
            {
                throw new AggregateException("Tab cache cleanup previously failed.", cacheCleanupErrors);
            }

            var enabled = definition.IsEnabled();
            ViewModel.SetEnabled(definition.Key, enabled);
            if (!enabled)
            {
                return new TabSelectionResult(TabSelectionStatus.Rejected, TabRejection.Disabled);
            }

            var previous = ViewModel.Snapshot;
            var previousDefinition = displayedDefinition;
            if (!forceReload && slot.Current != null && previous.Phase == TabPhase.Ready && previous.SelectedTab == definition.Key)
            {
                return new TabSelectionResult(TabSelectionStatus.Ready, error: previous.Error);
            }

            if (slot.Current != null && previousDefinition != null && previousDefinition.CanLeaveSynchronous != null)
            {
                var leave = new TabLeaveContext(previousDefinition.Key, definition.Key, slot.Current.Model);
                if (!previousDefinition.CanLeaveSynchronous(leave))
                {
                    return new TabSelectionResult(TabSelectionStatus.Rejected, TabRejection.Denied);
                }
            }

            scope.RequireActive();
            var requestVersion = ++version;
            ChildViewChangeResult result;
            try
            {
                InvalidateCacheSynchronous(false);
                result = slot.Replace(owner => PrepareSelectionSynchronous(definition, owner, forceReload),
                    _ =>
                    {
                        displayedDefinition = definition;
                        Publish(new TabSnapshot(definition.Key, definition.Key, TabPhase.Ready, null, requestVersion));
                    }, () => RequireSynchronousSelection(definition));
            }
            catch (Exception error)
            {
                result = new ChildViewChangeResult(ChildViewChangeStatus.Failed, error);
            }

            if (result.Status == ChildViewChangeStatus.Ready)
            {
                if (result.Error != null)
                {
                    Publish(new TabSnapshot(definition.Key, definition.Key, TabPhase.Ready, result.Error, requestVersion));
                    NotifySelectionFailure(new TabSelectionFailure(definition.Key, null, requestVersion, result.Error));
                }

                return new TabSelectionResult(TabSelectionStatus.Ready, error: result.Error);
            }

            var failure = result.Error ?? new InvalidOperationException("Synchronous Tab preparation did not complete.");
            var restored = failureDisplay == TabFailureDisplay.RestorePrevious && slot.Current != null &&
                previousDefinition != null && definitions.TryGetValue(previousDefinition.Key, out var currentDefinition) &&
                ReferenceEquals(previousDefinition, currentDefinition);
            if (restored)
            {
                try
                {
                    restored = previousDefinition.IsEnabled();
                }
                catch (Exception error)
                {
                    restored = false;
                    failure = CombineRecoveryFailure(failure, error);
                }
            }

            if (restored)
            {
                // 同步准备期间旧实例未停用，失败恢复无需重新打开或复制实例。
                displayedDefinition = previousDefinition;
                Publish(new TabSnapshot(previousDefinition.Key, previousDefinition.Key, TabPhase.Ready, null, requestVersion));
            }
            else
            {
                failure = ClearFailedContent(failure);
                Publish(new TabSnapshot(definition.Key, slot.Current == null ? null : previous.DisplayedTab,
                    TabPhase.Error, failure, requestVersion));
            }

            NotifySelectionFailure(new TabSelectionFailure(definition.Key,
                restored ? previousDefinition.Key : null, requestVersion, failure));
            return new TabSelectionResult(TabSelectionStatus.Failed, error: failure);
        }

        private void RequireSynchronousSelection(TabContentDefinition definition)
        {
            scope.RequireActive();
            if (inactive || !definitions.TryGetValue(definition.Key, out var current) ||
                !ReferenceEquals(current, definition) || !definition.IsEnabled())
            {
                throw new InvalidOperationException("Synchronous Tab selection is no longer available.");
            }

            scope.RequireActive();
        }

        private ChildViewHandle PrepareSelectionSynchronous(TabContentDefinition definition,
            ChildViewScope owner, bool forceReload)
        {
            RequireSynchronousSelection(definition);
            var prepared = TakeCachedSynchronous(definition, forceReload) ?? definition.PrepareSynchronous(owner);
            if (prepared == null || !prepared.BelongsTo(owner) || prepared.State != ChildViewState.Prepared)
            {
                throw new InvalidOperationException("Tab factory must return a Prepared handle from its supplied scope.");
            }

            try
            {
                TrackContent(prepared, definition);
                RequireSynchronousSelection(definition);
                return prepared;
            }
            catch (Exception failure)
            {
                try
                {
                    prepared.Dispose();
                }
                catch (Exception cleanup)
                {
                    throw ChildViewPreparationException.SynchronousCleanupFailed(failure, cleanup);
                }

                throw;
            }
        }

        /// <summary>同步关闭当前内容、逐项回收缓存并释放控制器，重复调用保留同一清理结果。</summary>
        public void Dispose()
        {
            RequireSynchronousMode();
            if (!CanDisposeSynchronously)
            {
                throw new InvalidOperationException("Tab controller cannot dispose synchronously during work or callbacks.");
            }

            if (!synchronousDisposalStarted)
            {
                synchronousDisposalStarted = true;
                var errors = new List<Exception>();
                void Cleanup(Action action)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }

                Cleanup(Deactivate);
                Cleanup(slot.Dispose);
                Cleanup(work.Dispose);
                var historicalCacheErrors = cacheCleanupErrors.Count;
                Cleanup(() => InvalidateCacheSynchronous(true));
                foreach (var handle in contentDefinitions.Keys)
                {
                    handle.Closed -= OnTrackedContentClosed;
                }

                contentDefinitions.Clear();
                // 本次回收错误已由上面的聚合异常收集，只额外合并历史失败。
                errors.AddRange(cacheCleanupErrors.GetRange(0, historicalCacheErrors));
                cacheCleanupErrors.Clear();
                retained = null;
                displayedDefinition = null;
                slot.CurrentChanged -= OnCurrentChanged;
                Cleanup(parentCancellation.Dispose);
                SelectionFailed = null;
                synchronousDisposalFailure = errors.Count == 0 ? null : new AggregateException("Synchronous Tab cleanup failed.", errors);
                synchronousDisposalCompleted = true;
            }

            if (synchronousDisposalFailure != null)
            {
                ExceptionDispatchInfo.Capture(synchronousDisposalFailure).Throw();
            }
        }
    }
}
