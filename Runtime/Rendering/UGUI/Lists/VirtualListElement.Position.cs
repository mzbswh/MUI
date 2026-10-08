using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private static readonly ConditionalWeakTable<object, object> PositionSourceIdentities =
            new ConditionalWeakTable<object, object>();
        private static readonly object EmptyPositionSourceIdentity = new object();

        private object PositionSourceIdentity => items == null ? EmptyPositionSourceIdentity :
            PositionSourceIdentities.GetValue(items, _ => new object());

        /// <summary>
        /// 捕获当前阅读位置和原顺序中的稳定键，时间及空间复杂度为 O(n)。
        /// 弹性越界不写入快照；仅在业务需要保存时调用，不应每帧捕获。
        /// </summary>
        public VirtualListPosition CapturePosition()
        {
            RequireListAlive();
            if (!initialized || scope == null || !scope.IsActive)
            {
                throw new InvalidOperationException("Capture position requires an active virtual list.");
            }

            var offset = UnityEngine.Mathf.Clamp(ScrollOffset, 0, EndOffset);
            var anchor = snapshot.Count == 0 ? -1 : Layout.FirstVisible(snapshot.Count, offset);
            var keys = new object[snapshot.Count];
            for (var i = 0; i < keys.Length; ++i)
            {
                keys[i] = snapshot[i].Key;
            }

            return new VirtualListPosition(PositionSourceIdentity, keys, anchor,
                anchor < 0 ? 0 : offset - Layout.OffsetForIndex(anchor));
        }

        /// <summary>
        /// 按当前布局恢复位置，包含异步物化及尺寸修正。不同来源默认拒绝；
        /// 仅当业务确认稳定键空间兼容时传入 allowCompatibleSource=true。
        /// 原锚点删除后依次寻找原顺序的后继、前驱，均不存在则回到起点。
        /// </summary>
        public async Task<VirtualListRestoreOutcome> RestorePositionAsync(VirtualListPosition position,
            bool allowCompatibleSource = false, CancellationToken cancellationToken = default)
        {
            RequireListAlive();
            RequireExternalPreparationWait();
            if (position == null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            VirtualListRestoreOutcome Result(VirtualListRevealStatus status) =>
                new VirtualListRestoreOutcome(RevealResult(status), false);
            if (scope == null || !scope.IsActive)
            {
                return Result(VirtualListRevealStatus.Inactive);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Result(VirtualListRevealStatus.Cancelled);
            }

            if (!allowCompatibleSource && !ReferenceEquals(position.SourceIdentity, PositionSourceIdentity))
            {
                return Result(VirtualListRevealStatus.IncompatibleSource);
            }

            var revision = sourceRevision;
            var request = revealRevision;
            var revealSource = revealSourceRevision;
            var activation = lifetime;
            object target = null;
            var offset = position.Offset;
            var fallback = false;
            try
            {
                if (position.AnchorKey != null)
                {
                    if (keyIndices.ContainsKey(position.AnchorKey))
                    {
                        target = position.AnchorKey;
                    }
                    else
                    {
                        fallback = true;
                        offset = 0;
                        for (var i = position.AnchorIndex + 1; target == null && i < position.Count; ++i)
                        {
                            if (keyIndices.ContainsKey(position.KeyAt(i)))
                            {
                                target = position.KeyAt(i);
                            }
                            if (revision != sourceRevision || request != revealRevision || !IsAlive)
                            {
                                break;
                            }
                        }
                        for (var i = position.AnchorIndex - 1; target == null && i >= 0; --i)
                        {
                            if (revision != sourceRevision || request != revealRevision || !IsAlive)
                            {
                                break;
                            }
                            if (keyIndices.ContainsKey(position.KeyAt(i)))
                            {
                                target = position.KeyAt(i);
                            }
                        }
                    }
                }

                // 自定义键比较可能重入，禁止使用重入前的候选结果启动新定位。
                var invalid = InvalidRevealStatus(request, revealSource, activation, cancellationToken);
                if (invalid.HasValue)
                {
                    return Result(invalid.Value);
                }
                if (revision != sourceRevision)
                {
                    return Result(VirtualListRevealStatus.Superseded);
                }

                var operation = await RevealAsync(target, false, cancellationToken,
                    VirtualListAlignment.Start, 0, offset);
                return new VirtualListRestoreOutcome(operation, fallback);
            }
            catch (Exception error)
            {
                return new VirtualListRestoreOutcome(
                    new VirtualListRevealOutcome(VirtualListRevealStatus.Failed, error), fallback);
            }
        }
    }
}
