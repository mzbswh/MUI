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
        }

        private void ParentCancelled(Lifetime activation)
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
            else if (activation.Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("Synchronous list cancellation requires its UI thread.");
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
            RequireAsyncListAllowed();
            RequireExternalPreparationWait();
            if (scope == null || !scope.IsActive)
            {
                throw new InvalidOperationException("Virtual list activation is inactive.");
            }

            if (running)
            {
                return pending ?? Task.CompletedTask;
            }

            try
            {
                ApplySnapshot(ReadSnapshot(items));
                return pending ?? Task.CompletedTask;
            }
            catch (Exception failure)
            {
                SetStatus(VirtualListStatus.Error, failure);
                return Task.FromException(failure);
            }
        }

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
