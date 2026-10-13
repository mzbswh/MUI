using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField, Min(1)]
        private int maxRevealCorrections = 64;
        private RevealControl activeReveal;
        private VirtualListScrollInput scrollInput;

        /// <summary>定位动画结束后的最大布局修正次数；达到上限返回 Failed 并保留当前位置。</summary>
        public int MaxRevealCorrections
        {
            get => maxRevealCorrections;
            set
            {
                RequireListAlive();
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                maxRevealCorrections = value;
            }
        }

        internal bool CanInterruptScroll
        {
            get
            {
                if (!IsAlive || scope == null || !scope.IsActive || scrollRect == null || !scrollRect.isActiveAndEnabled)
                {
                    return false;
                }

                var view = GetComponentInParent<View>(true);
                return view != null && view.IsInputEnabled;
            }
        }

        internal void InterruptScroll() => StopReveal(VirtualListRevealStatus.Cancelled);

        private void StopReveal(VirtualListRevealStatus reason)
        {
            var previous = activeReveal;
            activeReveal = null;
            if (previous != null)
            {
                previous.Stop(reason);
            }
        }

        private void InitializeScrollInput()
        {
            if (scrollRect.GetComponent<VirtualListScrollInput>() != null)
            {
                throw new InvalidOperationException("A ScrollRect cannot be owned by multiple virtual lists.");
            }

            scrollInput = scrollRect.gameObject.AddComponent<VirtualListScrollInput>();
            scrollInput.Attach(this);
            var owner = GetComponentInParent<View>(true);
            if (owner != null)
            {
                // 列表拥有扫描边界内的 ScrollRect；父 View 的控件扫描不会进入此区域。
                owner.AttachNativeGesture(scrollRect.transform);
                owner.InputGesturesInvalidated += InvalidateFocusReveal;
                OnDispose(() =>
                {
                    if (owner != null)
                    {
                        owner.InputGesturesInvalidated -= InvalidateFocusReveal;
                    }
                });
            }
            OnDispose(() =>
            {
                StopReveal(VirtualListRevealStatus.Inactive);
                if (scrollInput != null)
                {
                    scrollInput.Attach(null);
                    Destroy(scrollInput);
                    scrollInput = null;
                }
            });
        }

        private void InvalidateFocusReveal()
        {
            if (activeReveal != null && activeReveal.FocusRequest != null)
            {
                StopReveal(VirtualListRevealStatus.InputBlocked);
            }
        }

        private static void ValidateScrollDuration(float duration)
        {
            if (duration < 0 || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }
        }

        private async Task<VirtualListRevealOutcome?> AnimateRevealAsync(object key, VirtualListAlignment alignment,
            float duration, long request, long revision, LifetimeScope activation, CancellationToken token)
        {
            if (duration == 0)
            {
                return null;
            }

            var started = Time.unscaledTimeAsDouble;
            var origin = ScrollOffset;
            while (true)
            {
                var invalid = ResolveRevealIndex(key, request, revision, activation, token, out var index);
                if (invalid.HasValue)
                {
                    return RevealResult(invalid.Value);
                }

                if (Error != null)
                {
                    return new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, Error);
                }

                var target = RevealOffset(index, alignment);
                var progress = Mathf.Clamp01((float)((Time.unscaledTimeAsDouble - started) / duration));
                if (progress >= 1 || Math.Abs(target - ScrollOffset) <= 0.5f)
                {
                    return null;
                }

                // 插值以非缩放时间推进；准备中的条目不阻塞后续滚动帧。
                var eased = progress * progress * (3 - 2 * progress);
                SetOffset(Mathf.LerpUnclamped(origin, target, eased));
                RequestRefresh();
                await WaitForLayoutFrameAsync(token);
            }
        }

        private sealed class RevealControl : IDisposable
        {
            private readonly CancellationTokenSource cancellation;

            public RevealControl(CancellationToken caller, CancellationToken activation)
            {
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(caller, activation);
                Token = cancellation.Token;
            }

            public CancellationToken Token
            {
                get;
            }

            public NativeFocusObserver.Request FocusRequest
            {
                get; set;
            }

            public VirtualListRevealStatus? StopReason
            {
                get; private set;
            }

            public void Stop(VirtualListRevealStatus reason)
            {
                if (StopReason.HasValue)
                {
                    return;
                }

                StopReason = reason;
                cancellation.Cancel();
            }

            public void Dispose() => cancellation.Dispose();
        }
    }
}
