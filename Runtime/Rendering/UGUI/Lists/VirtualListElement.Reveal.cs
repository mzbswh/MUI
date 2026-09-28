using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private Dictionary<object, int> keyIndices = new Dictionary<object, int>();
        private bool intrinsicKeyIndex = true;
        private long sourceRevision;
        private long revealRevision;
        private long revealSourceRevision;
        private TaskCompletionSource<bool> layoutFrame;

        /// <summary>实例化指定键对应的行，可选地聚焦其第一个有效 Selectable。</summary>
        public async Task<VirtualListRevealOutcome> ScrollToKeyAsync(object key,
            bool focus = false,
            CancellationToken cancellationToken = default)
        {
            RequireListAlive();
            RequireAsyncListAllowed();
            RequireExternalPreparationWait();
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("List reveal must run on its UI thread.");
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (scope == null || !scope.IsActive)
            {
                return RevealResult(VirtualListRevealStatus.Inactive);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return RevealResult(VirtualListRevealStatus.Cancelled);
            }

            var activation = lifetime;
            var source = revealSourceRevision;
            var precedingRequest = revealRevision;
            var found = keyIndices.TryGetValue(key, out var index);
            if (!IsAlive || scope == null || !scope.IsActive || !ReferenceEquals(lifetime, activation))
            {
                return RevealResult(VirtualListRevealStatus.Inactive);
            }

            if (source != revealSourceRevision || precedingRequest != revealRevision)
            {
                return RevealResult(VirtualListRevealStatus.Superseded);
            }

            if (!found)
            {
                return RevealResult(VirtualListRevealStatus.NotFound);
            }

            var request = ++revealRevision;
            var revision = revealSourceRevision;
            try
            {
                // 数据更新会淘汰请求；同一来源的测量修正只要求重新对齐，不应冒充数据换代。
                var aligned = false;
                var passes = (long)maxCells + 8;
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activation.Token))
                {
                    for (long pass = 0; pass < passes; ++pass)
                    {
                        var invalid = InvalidRevealStatus(request, revision, activation, cancellationToken);
                        if (invalid.HasValue)
                        {
                            return RevealResult(invalid.Value);
                        }

                        var top = Layout.OffsetForIndex(index);
                        var current = scrollRect.content.anchoredPosition.y;
                        var height = scrollRect.viewport.rect.height;
                        if (top < current || ItemHeight(index) > height)
                        {
                            SetOffset(top);
                        }
                        else if (top + ItemHeight(index) > current + height)
                        {
                            SetOffset(top + ItemHeight(index) - height);
                        }

                        RequestRefresh();
                        if (pending != null)
                        {
                            await WaitForRevealAsync(pending, linked.Token);
                        }
                        if (focus)
                        {
                            await Task.Yield();
                        }

                        invalid = InvalidRevealStatus(request, revision, activation, cancellationToken);
                        if (invalid.HasValue)
                        {
                            return RevealResult(invalid.Value);
                        }

                        if (Error != null)
                        {
                            return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, Error);
                        }

                        var measurementsReady = VisibleMeasurementsReady();
                        invalid = InvalidRevealStatus(request, revision, activation, cancellationToken);
                        if (invalid.HasValue)
                        {
                            return RevealResult(invalid.Value);
                        }

                        var alignedNow = measurementsReady && IsRevealAligned(index);
                        invalid = InvalidRevealStatus(request, revision, activation, cancellationToken);
                        if (invalid.HasValue)
                        {
                            return RevealResult(invalid.Value);
                        }

                        if (alignedNow)
                        {
                            aligned = true;
                            break;
                        }

                        await WaitForLayoutFrameAsync(linked.Token);
                    }
                }

                if (!aligned)
                {
                    return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed,
                        new TimeoutException("Virtual list layout did not stabilize within its reveal budget."));
                }

                return CompleteReveal(key, focus, request, revision, activation, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return RevealResult(activation.IsEnded ? VirtualListRevealStatus.Inactive : VirtualListRevealStatus.Cancelled);
            }
            catch (Exception failure)
            {
                return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, failure);
            }
        }

        private VirtualListRevealOutcome CompleteReveal(object key, bool focus, long request, long revision,
            Lifetime activation, CancellationToken token)
        {
            Cell cell = null;
            for (var i = 0; i < cells.Count; i++)
            {
                var candidate = cells[i];
                var candidateKey = candidate.Key;
                var matches = Equals(candidateKey, key);
                var invalid = InvalidRevealStatus(request, revision, activation, token);
                if (invalid.HasValue)
                {
                    return RevealResult(invalid.Value);
                }

                if (i >= cells.Count || !ReferenceEquals(cells[i], candidate) || !ReferenceEquals(candidate.Key, candidateKey))
                {
                    return RevealResult(VirtualListRevealStatus.Superseded);
                }

                if (matches)
                {
                    cell = candidate;
                    break;
                }
            }

            if (cell == null || cell.Root == null || !cell.Root.gameObject.activeSelf)
            {
                return RevealResult(VirtualListRevealStatus.Superseded);
            }

            if (!focus)
            {
                return RevealResult(VirtualListRevealStatus.Ready);
            }

            var view = cell.Root.GetComponentInChildren<View>();
            if (view == null || !view.IsInputEnabled)
            {
                return RevealResult(VirtualListRevealStatus.InputBlocked);
            }

            var system = EventSystem.current;
            if (system == null || system.alreadySelecting)
            {
                return RevealResult(VirtualListRevealStatus.InputBlocked);
            }

            foreach (var selectable in cell.Root.GetComponentsInChildren<Selectable>())
            {
                if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
                {
                    continue;
                }

                system.SetSelectedGameObject(selectable.gameObject);
                // 原生选择处理器可能同步替换来源或关闭父级。
                if (!IsAlive || !ReferenceEquals(lifetime, activation) || activation.IsEnded)
                {
                    return RevealResult(VirtualListRevealStatus.Inactive);
                }

                if (request != revealRevision || revision != revealSourceRevision)
                {
                    return RevealResult(VirtualListRevealStatus.Superseded);
                }

                return RevealResult(system.currentSelectedGameObject == selectable.gameObject ? VirtualListRevealStatus.Ready : VirtualListRevealStatus.InputBlocked);
            }

            return RevealResult(VirtualListRevealStatus.NoSelectable);
        }

        private VirtualListRevealStatus? InvalidRevealStatus(long request, long revision,
                    Lifetime activation, CancellationToken token)
        {
            if (!IsAlive || scope == null || !scope.IsActive || !ReferenceEquals(lifetime, activation))
            {
                return VirtualListRevealStatus.Inactive;
            }

            if (token.IsCancellationRequested)
            {
                return VirtualListRevealStatus.Cancelled;
            }

            return request != revealRevision || revision != revealSourceRevision
                ? VirtualListRevealStatus.Superseded : (VirtualListRevealStatus?)null;
        }

        private bool IsRevealAligned(int index)
        {
            var top = Layout.OffsetForIndex(index);
            var offset = scrollRect.content.anchoredPosition.y;
            var viewport = scrollRect.viewport.rect.height;
            var height = ItemHeight(index);
            if (viewport <= 0)
            {
                return false;
            }

            return height > viewport ? Math.Abs(top - offset) <= 0.5f :
                top >= offset - 0.5f && top + height <= offset + viewport + 0.5f;
        }

        private bool VisibleMeasurementsReady()
        {
            if (!measureItemHeights)
            {
                return true;
            }

            if (measurementWidth != scrollRect.content.rect.width / columns || IsVisualRetentionActive)
            {
                return false;
            }

            var owner = scope;
            var revision = sourceRevision;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var key = cell.Key;
                if (key == null)
                {
                    continue;
                }

                var found = keyIndices.TryGetValue(key, out var index);
                if (!IsMeasurementSourceCurrent(owner, revision))
                {
                    return false;
                }

                if (!found)
                {
                    continue;
                }

                if (!ReferenceEquals(cell.Key, key) || index < 0 || index >= snapshot.Count)
                {
                    return false;
                }

                var item = snapshot[index];
                if (item.Height.HasValue)
                {
                    continue;
                }

                var measured = measuredItems.ContainsKey(item);
                if (!IsMeasurementSourceCurrent(owner, revision) || !ReferenceEquals(cell.Key, key) ||
                    index >= snapshot.Count || !ReferenceEquals(snapshot[index], item))
                {
                    return false;
                }

                if (!measured)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task WaitForLayoutFrameAsync(CancellationToken token)
        {
            if (layoutFrame == null)
            {
                layoutFrame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            var frame = layoutFrame.Task;
            using (var timer = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var deadline = Task.Delay(TimeSpan.FromSeconds(2), timer.Token);
                if (await Task.WhenAny(frame, deadline) != frame)
                {
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("Virtual list stopped receiving layout frames during reveal.");
                }

                timer.Cancel();
                await frame;
            }
        }

        private void CompleteLayoutFrame()
        {
            var previous = layoutFrame;
            layoutFrame = null;
            if (previous != null)
            {
                previous.TrySetResult(true);
            }
        }

        private static VirtualListRevealOutcome RevealResult(VirtualListRevealStatus status) => new VirtualListRevealOutcome(status);

        private static async Task WaitForRevealAsync(Task operation, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (operation.IsCompleted)
            {
                await operation;
                return;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(operation, cancelled.Task) != operation)
                {
                    token.ThrowIfCancellationRequested();
                }

                await operation;
            }
        }
    }
}
