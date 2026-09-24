using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;

namespace MUI.Tabs
{
    public sealed partial class TabContentController
    {
        /// <summary>
        /// 当前切换失败后的独立通知，包含可选的恢复键。观察者异常隔离；
        /// 通知中不能重入选择或目录更新，可由项目转交自己的提示服务。
        /// </summary>
        public event Action<TabSelectionFailure> SelectionFailed;

        private async ValueTask<TabSelectionResult> ResolveFailureAsync(Operation target, Exception failure)
        {
            var saved = retained;
            var restored = false;
            if (failureDisplay == TabFailureDisplay.RestorePrevious && saved != null &&
                saved.Handle.State == ChildViewState.Retained && !target.Cancellation.IsCancellationRequested)
            {
                try
                {
                    // 复用 Slot 的有界调度，先完成失败候选清理，再准备旧实例的新激活。
                    var recovery = await slot.ReplaceAsync(
                        (owner, token) => PrepareRecoveryAsync(target, saved, owner, token),
                        target.Cancellation.Token,
                        _ => CommitRecovery(target, saved), () => RequireRecoveryCurrent(target, saved));
                    restored = recovery.Status == ChildViewChangeStatus.Ready;
                    if (recovery.Error != null)
                    {
                        failure = CombineRecoveryFailure(failure, recovery.Error);
                    }
                }
                catch (Exception recovery)
                {
                    failure = CombineRecoveryFailure(failure, recovery);
                }
            }

            // 恢复期间的新选择、目录变化与父关闭均撤销发布权。
            if (!IsCurrent(target))
            {
                return new TabSelectionResult(inactive || !scope.IsActive
                    ? TabSelectionStatus.ParentInactive : TabSelectionStatus.Superseded);
            }

            if (!restored)
            {
                var clearingFailure = ClearFailedContent(null);
                if (clearingFailure != null)
                {
                    failure = CombineRecoveryFailure(failure, clearingFailure);
                }
                if (!IsCurrent(target))
                {
                    return new TabSelectionResult(inactive || !scope.IsActive
                        ? TabSelectionStatus.ParentInactive : TabSelectionStatus.Superseded);
                }

                if (target.Cancellation.IsCancellationRequested)
                {
                    Publish(new TabSnapshot(target.Key, null, clearingFailure == null ? TabPhase.Empty : TabPhase.Error, clearingFailure, target.Version));
                    return new TabSelectionResult(TabSelectionStatus.Cancelled, error: clearingFailure);
                }

                Publish(new TabSnapshot(target.Key, null, TabPhase.Error, failure, target.Version));
            }

            if (IsCurrent(target))
            {
                NotifySelectionFailure(new TabSelectionFailure(target.Key,
                    restored ? saved.Definition.Key : null, target.Version, failure));
            }

            // 恢复成功只修复显示，原目标的失败结果仍保留给调用者。
            return new TabSelectionResult(TabSelectionStatus.Failed, error: failure);
        }

        private async ValueTask<ChildViewHandle> PrepareRecoveryAsync(
            Operation target,
            RetainedContent saved,
            ChildViewScope owner,
            CancellationToken token)
        {
            var previous = preparing.Value;
            var frame = new PreparationFrame();
            preparing.Value = frame;
            ChildViewHandle prepared = null;
            try
            {
                RequireRecoveryCurrent(target, saved);
                token.ThrowIfCancellationRequested();
                Publish(new TabSnapshot(target.Key, null, TabPhase.Loading, null, target.Version,
                    pendingDisplay == TabPendingDisplay.LoadingPlaceholder && ViewModel.Snapshot.LoadingIndicatorVisible));
                prepared = await owner.PrepareReactivationAsync(saved.Handle, token);
                RequireRecoveryCurrent(target, saved);
                token.ThrowIfCancellationRequested();
                return prepared;
            }
            catch (Exception failure)
            {
                // 准备完成后的定义复核也可能失败；返回给 Slot 前仍由此处负责清理。
                if (prepared != null)
                {
                    try
                    {
                        await prepared.BeginClose();
                    }
                    catch (Exception cleanup)
                    {
                        throw new AggregateException("Recovered Tab validation and cleanup failed.", failure, cleanup);
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

        private void RequireRecoveryCurrent(Operation target, RetainedContent saved)
        {
            scope.RequireThread();
            if (!IsCurrent(target) || !ReferenceEquals(retained, saved) ||
                !definitions.TryGetValue(saved.Definition.Key, out var definition) ||
                !ReferenceEquals(definition, saved.Definition))
            {
                throw new OperationCanceledException("Previous Tab content is no longer eligible for recovery.");
            }

            var enabled = saved.Definition.IsEnabled();
            if (!enabled || !IsCurrent(target) || !ReferenceEquals(retained, saved))
            {
                throw new OperationCanceledException("Previous Tab content became unavailable during recovery.");
            }
        }

        private void CommitRecovery(Operation target, RetainedContent saved)
        {
            RequireRecoveryCurrent(target, saved);
            if (!ReferenceEquals(slot.Current, saved.Handle))
            {
                throw new InvalidOperationException("Recovered Tab is not the current slot content.");
            }

            retained = null;
            displayedDefinition = saved.Definition;
            Publish(new TabSnapshot(saved.Definition.Key, saved.Definition.Key, TabPhase.Ready, null, target.Version));
        }

        private void NotifySelectionFailure(TabSelectionFailure failure)
        {
            var handlers = SelectionFailed;
            if (handlers == null)
            {
                return;
            }

            var previous = publishing;
            publishing = true;
            try
            {
                foreach (Action<TabSelectionFailure> handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler(failure);
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            }
            finally
            {
                publishing = previous;
            }
        }

        private static Exception CombineRecoveryFailure(Exception original, Exception recovery)
        {
            return original == null ? recovery : new AggregateException("Tab selection and previous content recovery failed.", original, recovery);
        }
    }
}
