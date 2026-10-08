using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField]
        private GameObject loadingState = null;
        [SerializeField]
        private GameObject emptyState = null;
        [SerializeField]
        private GameObject errorState = null;
        private CancellationTokenRegistration parentCancellation;
        private SynchronizationContext uiContext;
        private int uiThread;

        public VirtualListStatus Status { get; private set; } = VirtualListStatus.Inactive;

        public Exception Error
        {
            get; private set;
        }

        /// <summary>初始化后先检查所属线程，再访问 Unity 对象或修改列表状态。</summary>
        private void RequireListAlive()
        {
            if (initialized && Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("虚拟列表只能在所属 UI 线程调用。");
            }

            RequireAlive();
            if (resettingActivation)
            {
                throw new InvalidOperationException("虚拟列表重置父激活期间不能修改列表。");
            }
        }

        private void ParentCancelled(LifetimeScope activation)
        {
            void Apply()
            {
                if (IsAlive && ReferenceEquals(lifetime, activation))
                {
                    SetStatus(VirtualListStatus.Inactive);
                }
            }

            if (Thread.CurrentThread.ManagedThreadId == uiThread)
            {
                Apply();
            }

            else if (uiContext != null)
            {
                uiContext.Post(_ => Apply(), null);
            }
            // 没有 UI 同步上下文的宿主，仍在 UI 线程销毁时完成更新。
        }

        public void ConfigureStates(GameObject loading, GameObject empty, GameObject error)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Configure list state nodes before initialization.");
            }

            loadingState = loading;
            emptyState = empty;
            errorState = error;
        }

        /// <summary>重新校验当前来源并重试实例化；并发重试共享当前操作。</summary>
        public Task RetryAsync()
        {
            RequireListAlive();
            RequireExternalPreparationWait();
            if (scope == null || !scope.IsActive)
            {
                throw new InvalidOperationException("Virtual list activation is inactive.");
            }

            if (running)
            {
                return pending ?? Task.CompletedTask;
            }

            var activation = lifetime;
            var assignment = itemsAssignmentVersion;
            var source = items;
            var revision = sourceRevision;
            var previousPending = pending;
            var applying = false;
            try
            {
                var version = source == null ? -1 : source.Version;
                if (!IsCurrentRetry(activation, assignment, source, revision))
                {
                    return Task.FromCanceled(new CancellationToken(true));
                }

                var next = ReadSnapshot(source);
                if (!IsCurrentRetry(activation, assignment, source, revision))
                {
                    return Task.FromCanceled(new CancellationToken(true));
                }

                var offsets = PrepareSnapshot(next);
                if (!IsCurrentRetry(activation, assignment, source, revision))
                {
                    return Task.FromCanceled(new CancellationToken(true));
                }

                if (source != null && source.Version != version)
                {
                    throw new InvalidOperationException("虚拟列表来源在重试读取期间发生静默变更，请重新发布集合通知。");
                }

                if (!IsCurrentRetry(activation, assignment, source, revision))
                {
                    return Task.FromCanceled(new CancellationToken(true));
                }

                applying = true;
                ApplyPreparedSnapshot(next, offsets, version);
                if (!IsCurrentItemsAssignment(assignment, activation) || !ReferenceEquals(items, source))
                {
                    return Task.FromCanceled(new CancellationToken(true));
                }

                return pending ?? Task.CompletedTask;
            }
            catch (Exception failure)
            {
                var failed = Task.FromException(failure);
                _ = failed.Exception;
                if (IsCurrentItemsAssignment(assignment, activation) && ReferenceEquals(items, source) &&
                    (sourceRevision == revision || (applying && sourceRevision == revision + 1)) &&
                    ReferenceEquals(pending, previousPending))
                {
                    pending = failed;
                    SetStatus(VirtualListStatus.Error, failure);
                }

                return failed;
            }
        }

        private bool IsCurrentRetry(LifetimeScope activation, long assignment,
            IReadOnlyObservableList<VirtualListItem> source, long revision) =>
            IsCurrentItemsAssignment(assignment, activation) && ReferenceEquals(items, source) &&
            sourceRevision == revision;

        private void ValidateStateNodes()
        {
            var nodes = new[]
            {
                loadingState,
                emptyState,
                errorState
            };
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                if (node.transform == transform || !node.transform.IsChildOf(transform) || node.transform.IsChildOf(scrollRect.content) || scrollRect.transform.IsChildOf(node.transform) || node.transform.IsChildOf(itemTemplate.transform) || itemTemplate.transform.IsChildOf(node.transform))
                {
                    throw new InvalidOperationException("List state nodes must be independent descendants outside content and template.");
                }

                foreach (var template in templatesByKey.Values)
                {
                    if (node.transform.IsChildOf(template.transform) || template.transform.IsChildOf(node.transform))
                    {
                        throw new InvalidOperationException("List state nodes must be outside named item templates.");
                    }
                }

                for (var j = 0; j < i; j++)
                {
                    if (nodes[j] != null && (node.transform.IsChildOf(nodes[j].transform) || nodes[j].transform.IsChildOf(node.transform)))
                    {
                        throw new InvalidOperationException("List state nodes must be distinct and cannot contain each other.");
                    }
                }
            }
        }

        private void SetStatus(VirtualListStatus status, Exception error = null)
        {
            var statusChanged = Status != status;
            var errorChanged = !ReferenceEquals(Error, error);
            Status = status;
            Error = error;
            if (!IsVisualRetentionActive)
            {
                ApplyStatusNodes();
            }
            try
            {
                if (statusChanged)
                {
                    NotifyChanged(nameof(Status));
                }

                if (errorChanged)
                {
                    NotifyChanged(nameof(Error));
                }
            }
            catch (Exception observerError)
            {
                UIErrors.Report(observerError);
            }
        }

        protected override void OnVisualRetentionEnded()
        {
            ApplyStatusNodes();
        }

        private void ApplyStatusNodes()
        {
            ApplyStateNode(loadingState, Status == VirtualListStatus.Loading);
            ApplyStateNode(emptyState, Status == VirtualListStatus.Empty);
            ApplyStateNode(errorState, Status == VirtualListStatus.Error);
        }

        private static void ApplyStateNode(GameObject node, bool visible)
        {
            if (node != null && node.activeSelf != visible)
            {
                node.SetActive(visible);
            }
        }
    }
}
