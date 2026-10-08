using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField] private GameObject itemFailureTemplate;
        // 失败与画面按当前不可变条目索引，内部清理不执行项目稳定键的比较回调。
        private readonly Dictionary<VirtualListItem, VirtualListItemFailure> itemFailures = new Dictionary<VirtualListItem, VirtualListItemFailure>();
        private readonly Dictionary<VirtualListItem, RectTransform> failurePlaceholders = new Dictionary<VirtualListItem, RectTransform>();

        /// <summary>条目失败通知。观察者异常单独报告，不中断其余条目的准备。</summary>
        public event Action<VirtualListItemFailure> ItemFailed;

        /// <summary>当前来源的失败条目数量；占位不影响其他条目的 Ready 状态。</summary>
        public int FailedItemCount => itemFailures.Count;

        /// <summary>配置无交互的失败画面模板；未配置时保留有效空白占位尺寸。</summary>
        public void ConfigureItemFailureTemplate(GameObject template)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Configure failure presentation before initialization.");
            }

            itemFailureTemplate = template;
        }

        /// <summary>查询当前来源与当前条目内容的失败；不存在或已换源时返回 null。</summary>
        public VirtualListItemFailure GetItemFailure(object key)
        {
            RequireListAlive();
            var revision = sourceRevision;
            var activation = lifetime;
            if (key == null || !keyIndices.TryGetValue(key, out var index) || !IsAlive ||
                revision != sourceRevision || !ReferenceEquals(lifetime, activation) ||
                index < 0 || index >= snapshot.Count || !itemFailures.TryGetValue(snapshot[index], out var failure) ||
                failure.SourceGeneration != revealSourceRevision)
            {
                return null;
            }

            return failure;
        }

        /// <summary>显式清除指定键的失败并重新物化；复用定位流程的取消、并发上限和代际检查。</summary>
        public Task<VirtualListRevealOutcome> RetryItemAsync(object key, CancellationToken cancellationToken = default)
        {
            RequireListAlive();
            RequireExternalPreparationWait();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (scope == null || !scope.IsActive)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Inactive));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Cancelled));
            }

            var activation = lifetime;
            var generation = revealSourceRevision;
            var revision = sourceRevision;
            var found = keyIndices.TryGetValue(key, out var index);
            if (!IsAlive || !ReferenceEquals(lifetime, activation) || activation.IsEnded)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Inactive));
            }

            if (sourceRevision != revision)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Superseded));
            }

            if (!found)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.NotFound));
            }

            var item = snapshot[index];
            var removed = itemFailures.Remove(item);
            RemoveFailurePlaceholder(item);
            if (removed)
            {
                NotifyFailureCount();
            }

            if (!IsAlive || !ReferenceEquals(lifetime, activation) || activation.IsEnded)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Inactive));
            }

            if (revealSourceRevision != generation || sourceRevision != revision)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Superseded));
            }

            first = last = -1;
            return RevealAsync(key, false, cancellationToken, VirtualListAlignment.Nearest, 0);
        }

        private void ValidateItemFailureTemplate()
        {
            if (itemFailureTemplate == null)
            {
                return;
            }

            var node = itemFailureTemplate.transform;
            if (!(node is RectTransform) || itemFailureTemplate.activeSelf || !node.IsChildOf(transform) ||
                node.IsChildOf(scrollRect.content) || scrollRect.content.IsChildOf(node) ||
                node.IsChildOf(itemTemplate.transform) || itemTemplate.transform.IsChildOf(node))
            {
                throw new InvalidOperationException("Failure template must be an inactive RectTransform inside the list boundary, outside content and item templates.");
            }

            foreach (var template in templatesByKey.Values)
            {
                if (node.IsChildOf(template.transform) || template.transform.IsChildOf(node))
                {
                    throw new InvalidOperationException("Failure template cannot overlap a named item template.");
                }
            }

            if (itemFailureTemplate.GetComponentInChildren<View>(true) != null ||
                itemFailureTemplate.GetComponentInChildren<Element>(true) != null)
            {
                throw new InvalidOperationException("Failure presentation must be a visual template without View or Element ownership.");
            }

            foreach (var group in itemFailureTemplate.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (group.ignoreParentGroups)
                {
                    throw new InvalidOperationException("Failure presentation cannot bypass its non-interactive parent CanvasGroup.");
                }
            }
        }

        private void RecordItemFailure(VirtualListItem item, Exception error)
        {
            var failure = new VirtualListItemFailure(item, revealSourceRevision, error);
            itemFailures[item] = failure;
            NotifyFailureCount();
            UIErrors.Report(error);
            var observers = ItemFailed;
            if (observers == null)
            {
                return;
            }

            foreach (Action<VirtualListItemFailure> observer in observers.GetInvocationList())
            {
                try
                {
                    observer(failure);
                }
                catch (Exception observerError)
                {
                    UIErrors.Report(observerError);
                }
            }
        }

        private void PruneItemFailures()
        {
            var revision = sourceRevision;
            var activation = lifetime;
            var current = new HashSet<VirtualListItem>(snapshot);
            var removed = new List<VirtualListItem>();
            foreach (var pair in itemFailures)
            {
                if (pair.Value.SourceGeneration != revealSourceRevision || !current.Contains(pair.Value.Item))
                {
                    removed.Add(pair.Key);
                }
            }

            foreach (var key in removed)
            {
                if (!IsAlive || revision != sourceRevision || !ReferenceEquals(lifetime, activation))
                {
                    return;
                }

                itemFailures.Remove(key);
                RemoveFailurePlaceholder(key);
            }

            if (removed.Count != 0)
            {
                NotifyFailureCount();
            }
        }

        /// <summary>只为当前视口建立失败画面；滚出范围即销毁，不积累历史物化节点。</summary>
        private void RefreshFailurePlaceholders(int start, int end, long revision, LifetimeScope activation)
        {
            var visible = new HashSet<VirtualListItem>();
            for (var index = start; index < end; ++index)
            {
                if (!IsRefreshCurrent(revision, activation))
                {
                    return;
                }

                var item = snapshot[index];
                itemFailures.TryGetValue(item, out var failure);
                if (!IsRefreshCurrent(revision, activation))
                {
                    return;
                }

                if (failure == null)
                {
                    continue;
                }

                visible.Add(item);
                if (!failurePlaceholders.TryGetValue(item, out var root) || root == null)
                {
                    root = new GameObject("Failed Virtual Item", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
                    failurePlaceholders[item] = root;
                    root.gameObject.SetActive(false);
                    root.SetParent(scrollRect.content, false);
                    if (!IsRefreshCurrent(revision, activation))
                    {
                        return;
                    }

                    var gate = root.GetComponent<CanvasGroup>();
                    gate.interactable = false;
                    gate.blocksRaycasts = false;
                    if (itemFailureTemplate != null)
                    {
                        var visual = Instantiate(itemFailureTemplate, root, false);
                        if (!IsRefreshCurrent(revision, activation) || root == null || visual == null)
                        {
                            return;
                        }

                        var rect = (RectTransform)visual.transform;
                        rect.anchorMin = Vector2.zero;
                        rect.anchorMax = Vector2.one;
                        rect.offsetMin = rect.offsetMax = Vector2.zero;
                        visual.SetActive(true);
                    }
                }

                if (!IsRefreshCurrent(revision, activation))
                {
                    return;
                }

                PositionCell(root, index, EffectiveExtent(item));
                if (!IsRefreshCurrent(revision, activation))
                {
                    return;
                }

                root.gameObject.SetActive(true);
            }

            if (!IsRefreshCurrent(revision, activation))
            {
                return;
            }

            var removed = new List<VirtualListItem>();
            foreach (var key in failurePlaceholders.Keys)
            {
                if (!visible.Contains(key))
                {
                    removed.Add(key);
                }
            }

            foreach (var key in removed)
            {
                if (!IsRefreshCurrent(revision, activation))
                {
                    return;
                }

                RemoveFailurePlaceholder(key);
            }
        }

        private void RemoveFailurePlaceholder(VirtualListItem key)
        {
            if (failurePlaceholders.TryGetValue(key, out var root))
            {
                failurePlaceholders.Remove(key);
                if (root != null)
                {
                    try
                    {
                        root.gameObject.SetActive(false);
                    }
                    finally
                    {
                        if (root != null)
                        {
                            Destroy(root.gameObject);
                        }
                    }
                }
            }
        }

        private void ClearItemFailures()
        {
            var changed = itemFailures.Count != 0;
            itemFailures.Clear();
            var roots = new List<RectTransform>(failurePlaceholders.Values);
            failurePlaceholders.Clear();
            foreach (var root in roots)
            {
                if (root != null)
                {
                    root.gameObject.SetActive(false);
                    Destroy(root.gameObject);
                }
            }

            if (changed)
            {
                NotifyFailureCount();
            }
        }

        private void NotifyFailureCount()
        {
            try
            {
                NotifyChanged(nameof(FailedItemCount));
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
