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

        /// <summary>在请求时将索引解析为稳定键；无效索引返回 NotFound。</summary>
        public Task<VirtualListRevealOutcome> ScrollToIndexAsync(int index, bool focus = false,
            CancellationToken cancellationToken = default,
            VirtualListAlignment alignment = VirtualListAlignment.Nearest, float duration = 0)
        {
            RequireListAlive();
            RequireExternalPreparationWait();
            ValidateAlignment(alignment);
            ValidateScrollDuration(duration);
            if (scope == null || !scope.IsActive)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Inactive));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Cancelled));
            }

            return index < 0 || index >= snapshot.Count
                ? Task.FromResult(RevealResult(VirtualListRevealStatus.NotFound))
                : ScrollToKeyAsync(snapshot[index].Key, focus, cancellationToken, alignment, duration);
        }

        /// <summary>实例化指定键对应的行，可选地聚焦其第一个有效 Selectable。</summary>
        public Task<VirtualListRevealOutcome> ScrollToKeyAsync(object key,
            bool focus = false,
            CancellationToken cancellationToken = default,
            VirtualListAlignment alignment = VirtualListAlignment.Nearest, float duration = 0)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return RevealAsync(key, focus, cancellationToken, alignment, duration);
        }

        private async Task<VirtualListRevealOutcome> RevealAsync(object key, bool focus,
            CancellationToken cancellationToken, VirtualListAlignment alignment, float duration,
            float? anchorOffset = null, string focusElementName = null,
            IReadOnlyList<string> focusControlPath = null)
        {
            RequireListAlive();
            RequireExternalPreparationWait();
            ValidateAlignment(alignment);
            ValidateScrollDuration(duration);
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("List reveal must run on its UI thread.");
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
            var index = -1;
            var found = key == null || keyIndices.TryGetValue(key, out index);
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
            if (scrollInput != null && scrollInput.IsDragging)
            {
                return RevealResult(VirtualListRevealStatus.Cancelled);
            }

            using var control = new RevealControl(cancellationToken, activation.Token);
            control.FocusRequest = focus ? NativeFocusObserver.Capture(View.GetInputEventSystem(transform)) : null;
            var token = control.Token;
            var previous = activeReveal;
            activeReveal = control;
            if (previous != null)
            {
                previous.Stop(VirtualListRevealStatus.Superseded);
            }

            try
            {
                // 增量更新与测量修正重新解析稳定键；只有来源重置或新请求使本次定位失效。
                var aligned = false;
                var passes = maxRevealCorrections;
                var animated = await AnimateRevealAsync(key, alignment, duration, request, revision, activation, token);
                if (animated.HasValue)
                {
                    return control.StopReason.HasValue ? RevealResult(control.StopReason.Value) : animated.Value;
                }

                for (long pass = 0; pass < passes; ++pass)
                {
                    var invalid = InvalidRevealStatus(request, revision, activation, token);
                    if (invalid.HasValue)
                    {
                        return RevealResult(control.StopReason ?? invalid.Value);
                    }

                    invalid = ResolveRevealIndex(key, request, revision, activation, token, out index);
                    if (invalid.HasValue)
                    {
                        return RevealResult(control.StopReason ?? invalid.Value);
                    }

                    SetOffset(RevealOffset(index, alignment, anchorOffset));

                    RequestRefresh();
                    if (pending != null)
                    {
                        await WaitForRevealAsync(pending, token);
                    }

                    invalid = InvalidRevealStatus(request, revision, activation, token);
                    if (invalid.HasValue)
                    {
                        return RevealResult(control.StopReason ?? invalid.Value);
                    }

                    if (Error != null)
                    {
                        return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, Error);
                    }

                    var itemFailure = GetItemFailure(key);
                    if (itemFailure != null)
                    {
                        return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, itemFailure.Error);
                    }

                    var measurementsReady = VisibleMeasurementsReady();
                    invalid = InvalidRevealStatus(request, revision, activation, token);
                    if (invalid.HasValue)
                    {
                        return RevealResult(control.StopReason ?? invalid.Value);
                    }

                    invalid = ResolveRevealIndex(key, request, revision, activation, token, out index);
                    if (invalid.HasValue)
                    {
                        return RevealResult(control.StopReason ?? invalid.Value);
                    }

                    var alignedNow = measurementsReady && IsRevealAligned(index, alignment, anchorOffset);
                    invalid = InvalidRevealStatus(request, revision, activation, token);
                    if (invalid.HasValue)
                    {
                        return RevealResult(control.StopReason ?? invalid.Value);
                    }

                    if (alignedNow)
                    {
                        aligned = true;
                        break;
                    }

                    await WaitForLayoutFrameAsync(token);
                }

                if (!aligned)
                {
                    return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed,
                        new TimeoutException("Virtual list layout did not stabilize within its reveal budget."));
                }

                return CompleteReveal(key, focus, request, revision, activation, token, focusElementName, focusControlPath);
            }
            catch (OperationCanceledException)
            {
                return RevealResult(activation.IsEnded ? VirtualListRevealStatus.Inactive : control.StopReason ?? VirtualListRevealStatus.Cancelled);
            }
            catch (Exception failure)
            {
                return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, failure);
            }
            finally
            {
                if (ReferenceEquals(activeReveal, control))
                {
                    activeReveal = null;
                }
            }
        }

        private VirtualListRevealOutcome CompleteReveal(object key, bool focus, long request, long revision,
            LifetimeScope activation, CancellationToken token, string focusElementName = null,
            IReadOnlyList<string> focusControlPath = null)
        {
            var initialInvalid = InvalidRevealStatus(request, revision, activation, token);
            if (initialInvalid.HasValue)
            {
                return RevealResult(initialInvalid.Value);
            }

            // 空快照或所有候选锚点已删除时，恢复到起点，无需目标条目。
            if (key == null)
            {
                return RevealResult(VirtualListRevealStatus.Ready);
            }

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
            if (view == null || !view.IsInputEnabled || !view.CanReceiveSharedKeyboardInput())
            {
                return RevealResult(VirtualListRevealStatus.InputBlocked);
            }

            var system = View.GetInputEventSystem(transform);
            if (system == null || system.alreadySelecting)
            {
                return RevealResult(VirtualListRevealStatus.InputBlocked);
            }

            var selectable = view.FindFocusTarget(focusElementName, focusControlPath);
            if (selectable == null)
            {
                // 条目没有有效控件时，继续按所属页面默认控件和层级顺序回退。
                var page = GetComponentInParent<View>(true);
                if (page != null && page.IsInputEnabled && page.CanReceiveSharedKeyboardInput())
                {
                    selectable = page.FindFocusTarget(null);
                }
            }
            var candidateInvalid = InvalidRevealStatus(request, revision, activation, token);
            if (candidateInvalid.HasValue)
            {
                return RevealResult(candidateInvalid.Value);
            }

            if (view == null || !view.IsInputEnabled || !view.CanReceiveSharedKeyboardInput())
            {
                return RevealResult(VirtualListRevealStatus.InputBlocked);
            }

            var focusRequest = activeReveal == null ? null : activeReveal.FocusRequest;
            if (focusRequest != null && !focusRequest.IsCurrent(system))
            {
                return RevealResult(VirtualListRevealStatus.Superseded);
            }

            if (selectable != null)
            {
                system.SetSelectedGameObject(selectable.gameObject);
                // 原生选择处理器可能同步替换来源或关闭父级。
                if (!IsAlive || !ReferenceEquals(lifetime, activation) || activation.IsEnded)
                {
                    ClearFailedFocus(system, selectable);
                    return RevealResult(VirtualListRevealStatus.Inactive);
                }

                if (request != revealRevision || revision != revealSourceRevision)
                {
                    return RevealResult(VirtualListRevealStatus.Superseded);
                }

                if (token.IsCancellationRequested || view == null || !view.IsInputEnabled ||
                    !view.CanReceiveSharedKeyboardInput())
                {
                    ClearFailedFocus(system, selectable);
                    return RevealResult(token.IsCancellationRequested
                        ? VirtualListRevealStatus.Cancelled : VirtualListRevealStatus.InputBlocked);
                }

                return RevealResult(system.currentSelectedGameObject == selectable.gameObject ? VirtualListRevealStatus.Ready : VirtualListRevealStatus.InputBlocked);
            }

            NativeFocusObserver.SetFrameworkSelection(system, null);
            return RevealResult(VirtualListRevealStatus.NoSelectable);
        }

        private static void ClearFailedFocus(EventSystem system, Selectable selected)
        {
            // 选择回调可能选中另一控件；只撤回本请求仍持有的选择，保留更新的用户焦点。
            if (system != null && !system.alreadySelecting && selected != null &&
                system.currentSelectedGameObject == selected.gameObject)
            {
                NativeFocusObserver.SetFrameworkSelection(system, null);
            }
        }

        private VirtualListRevealStatus? InvalidRevealStatus(long request, long revision,
                    LifetimeScope activation, CancellationToken token)
        {
            if (!IsAlive || scope == null || !scope.IsActive || !ReferenceEquals(lifetime, activation))
            {
                return VirtualListRevealStatus.Inactive;
            }

            if (token.IsCancellationRequested)
            {
                return VirtualListRevealStatus.Cancelled;
            }

            if (request == revealRevision && activeReveal != null && activeReveal.FocusRequest != null &&
                !activeReveal.FocusRequest.IsCurrent(View.GetInputEventSystem(transform)))
            {
                return VirtualListRevealStatus.Superseded;
            }

            return request != revealRevision || revision != revealSourceRevision
                ? VirtualListRevealStatus.Superseded : (VirtualListRevealStatus?)null;
        }

        /// <summary>外部等待或布局回调后重新解析目标；旧索引不能指向移动后的另一条数据。</summary>
        private VirtualListRevealStatus? ResolveRevealIndex(object key, long request, long revision,
            LifetimeScope activation, CancellationToken token, out int index)
        {
            index = -1;
            var invalid = InvalidRevealStatus(request, revision, activation, token);
            if (invalid.HasValue)
            {
                return invalid;
            }

            var dataRevision = sourceRevision;
            var found = key == null || keyIndices.TryGetValue(key, out index);
            invalid = InvalidRevealStatus(request, revision, activation, token);
            if (invalid.HasValue)
            {
                return invalid;
            }

            // 自定义键比较可能重入并改写来源；不能消费这次非一致查询的索引。
            if (dataRevision != sourceRevision)
            {
                return VirtualListRevealStatus.Superseded;
            }

            return found ? (VirtualListRevealStatus?)null : VirtualListRevealStatus.NotFound;
        }

        private static void ValidateAlignment(VirtualListAlignment alignment)
        {
            if (alignment < VirtualListAlignment.Nearest || alignment > VirtualListAlignment.End)
            {
                throw new ArgumentOutOfRangeException(nameof(alignment));
            }
        }

        /// <summary>沿滚动轴计算可实现的对齐位置；首尾条目受内容边界钳制。</summary>
        private float RevealOffset(int index, VirtualListAlignment alignment, float? anchorOffset = null)
        {
            if (index < 0)
            {
                return 0;
            }

            if (anchorOffset.HasValue)
            {
                return UnityEngine.Mathf.Clamp(Layout.OffsetForIndex(index) +
                    Math.Min(anchorOffset.Value, Layout.RowExtentForIndex(index)), 0, EndOffset);
            }

            var start = Layout.OffsetForIndex(index);
            var extent = ItemExtent(index);
            var viewport = ViewportExtent;
            var target = ScrollOffset;
            switch (alignment)
            {
                case VirtualListAlignment.Start:
                    target = start;
                    break;
                case VirtualListAlignment.Center:
                    target = start + (extent - viewport) * 0.5f;
                    break;
                case VirtualListAlignment.End:
                    target = start + extent - viewport;
                    break;
                default:
                    if (start < target || extent > viewport)
                    {
                        target = start;
                    }
                    else if (start + extent > target + viewport)
                    {
                        target = start + extent - viewport;
                    }
                    break;
            }

            return UnityEngine.Mathf.Clamp(target, 0, Math.Max(0, Layout.ContentExtent(snapshot.Count) - viewport));
        }

        private bool IsRevealAligned(int index, VirtualListAlignment alignment = VirtualListAlignment.Nearest,
            float? anchorOffset = null) =>
            ViewportExtent > 0 && Math.Abs(RevealOffset(index, alignment, anchorOffset) - ScrollOffset) <= 0.5f;

        private bool VisibleMeasurementsReady()
        {
            if (!UsesMeasuredExtents)
            {
                return true;
            }

            if (measurementCrossExtent != ItemCrossExtent || IsVisualRetentionActive)
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
                if (item.Extent.HasValue)
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
