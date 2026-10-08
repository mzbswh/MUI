using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        private readonly object focusOwnerIdentity = new object();

        internal bool UsesFocusDirection(MoveDirection direction) => IsHorizontal
            ? direction == MoveDirection.Left || direction == MoveDirection.Right
            : direction == MoveDirection.Up || direction == MoveDirection.Down ||
                (columns > 1 && (direction == MoveDirection.Left || direction == MoveDirection.Right));

        private void AttachNativeFocus(Cell cell, View itemView)
        {
            foreach (var selectable in itemView.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
            {
                if (selectable.GetComponentInParent<VirtualListElement>(true) != this)
                {
                    continue;
                }

                var relay = selectable.gameObject.AddComponent<VirtualListFocusInput>();
                cell.FocusInputs.Add(relay);
                relay.Attach(this, itemView, selectable);
            }
        }

        private static void DetachNativeFocus(Cell cell)
        {
            foreach (var relay in cell.FocusInputs)
            {
                if (relay != null)
                {
                    relay.Detach();
                    Destroy(relay);
                }
            }

            cell.FocusInputs.Clear();
        }

        /// <summary>捕获当前条目的逻辑焦点；原生选择不属于当前来源的已物化条目时返回 null。</summary>
        public VirtualListFocusPosition CaptureFocus()
        {
            RequireListAlive();
            var system = View.GetInputEventSystem(transform);
            var selected = system == null ? null : system.currentSelectedGameObject;
            if (selected == null || scope == null || !scope.IsActive)
            {
                return null;
            }

            foreach (var cell in cells)
            {
                if (cell.Key == null || cell.Root == null || cell.SourceGeneration != revealSourceRevision ||
                    !selected.transform.IsChildOf(cell.Root))
                {
                    continue;
                }

                var element = selected.GetComponent<Element>();
                var itemView = cell.Root.GetComponentInChildren<View>();
                var name = element != null && element.IsAlive && element.GetComponentInParent<View>(true) == itemView
                    ? element.Name : null;
                return new VirtualListFocusPosition(focusOwnerIdentity, revealSourceRevision, cell.Key, name,
                    itemView == null ? null : itemView.CaptureFocusPath(selected.transform));
            }

            return null;
        }

        /// <summary>物化并聚焦指定键；元素失效时按条目 View 的默认焦点规则回退。</summary>
        public Task<VirtualListRevealOutcome> FocusItemAsync(object key, string elementName = null,
            CancellationToken cancellationToken = default)
            => FocusItemCoreAsync(key, elementName, null, cancellationToken);

        private Task<VirtualListRevealOutcome> FocusItemCoreAsync(object key, string elementName,
            System.Collections.Generic.IReadOnlyList<string> controlPath, CancellationToken cancellationToken)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            RequireListAlive();
            if (scope == null || !scope.IsActive)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Inactive));
            }

            if (!CanInterruptScroll)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.InputBlocked));
            }

            return RevealAsync(key, true, cancellationToken, VirtualListAlignment.Nearest, 0,
                focusElementName: elementName, focusControlPath: controlPath);
        }

        /// <summary>恢复同一来源代际的稳定键与控件；换源、Reset 或父激活结束后拒绝旧快照。</summary>
        public Task<VirtualListRevealOutcome> RestoreFocusAsync(VirtualListFocusPosition position,
            CancellationToken cancellationToken = default)
        {
            RequireListAlive();
            if (position == null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (!ReferenceEquals(position.OwnerIdentity, focusOwnerIdentity) ||
                position.SourceGeneration != revealSourceRevision)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.IncompatibleSource));
            }

            return FocusItemCoreAsync(position.Key, position.ElementName, position.ControlPath, cancellationToken);
        }

        /// <summary>从当前条目按滚动轴或 Grid 行列移动焦点，并将目标滚入视口；行末不横向换行。</summary>
        public Task<VirtualListRevealOutcome> MoveFocusAsync(MoveDirection direction,
            CancellationToken cancellationToken = default)
        {
            RequireListAlive();
            if (!Enum.IsDefined(typeof(MoveDirection), direction))
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }

            if (!CanInterruptScroll)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.InputBlocked));
            }

            var position = CaptureFocus();
            if (position == null)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.NoSelectable));
            }

            var revision = sourceRevision;
            var activation = lifetime;
            var found = keyIndices.TryGetValue(position.Key, out var index);
            if (!IsAlive || !ReferenceEquals(lifetime, activation) || revision != sourceRevision)
            {
                return Task.FromResult(RevealResult(VirtualListRevealStatus.Superseded));
            }

            var next = found ? AdjacentFocusIndex(index, direction) : -1;
            return next < 0 || next >= snapshot.Count
                ? Task.FromResult(RevealResult(VirtualListRevealStatus.NotFound))
                : FocusItemCoreAsync(snapshot[next].Key, position.ElementName, position.ControlPath, cancellationToken);
        }

        private int AdjacentFocusIndex(int index, MoveDirection direction)
        {
            if (IsHorizontal)
            {
                return direction == MoveDirection.Left ? index - 1 : direction == MoveDirection.Right ? index + 1 : -1;
            }

            switch (direction)
            {
                case MoveDirection.Up:
                    return index - columns;
                case MoveDirection.Down:
                    return index > int.MaxValue - columns ? -1 : index + columns;
                case MoveDirection.Left:
                    return columns > 1 && index % columns > 0 ? index - 1 : -1;
                case MoveDirection.Right:
                    return columns > 1 && index % columns < columns - 1 ? index + 1 : -1;
                default:
                    return -1;
            }
        }
    }
}
