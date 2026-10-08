using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>保持条目内部的原生导航，在边界将方向输入交给稳定键定位，避免跟随池化节点。</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class VirtualListFocusInput : MonoBehaviour, IMoveHandler
    {
        private VirtualListElement owner;
        private View itemView;
        private Selectable target;
        private UnityEngine.UI.Navigation authoredNavigation;
        private UnityEngine.UI.Navigation heldNavigation;

        internal void Attach(VirtualListElement list, View view, Selectable selectable)
        {
            if (owner != null)
            {
                throw new InvalidOperationException("A selectable cannot belong to multiple virtual focus owners.");
            }

            owner = list;
            itemView = view;
            target = selectable;
            authoredNavigation = target.navigation;
            // 原生 Selectable 先收到事件时只保持当前选择；本适配随后分发唯一的实际移动。
            heldNavigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnLeft = target,
                selectOnRight = target,
                selectOnUp = target,
                selectOnDown = target
            };
            target.navigation = heldNavigation;
            if (authoredNavigation.mode == UnityEngine.UI.Navigation.Mode.None)
            {
                heldNavigation = authoredNavigation;
                target.navigation = heldNavigation;
            }
        }

        internal void Detach()
        {
            if (target != null && target.navigation.Equals(heldNavigation))
            {
                target.navigation = authoredNavigation;
            }

            owner = null;
            itemView = null;
            target = null;
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData == null || owner == null || itemView == null || target == null ||
                !owner.CanInterruptScroll || !itemView.IsInputEnabled || !itemView.CanReceiveSharedKeyboardInput() ||
                !target.isActiveAndEnabled || !target.IsInteractable())
            {
                return;
            }

            var system = View.GetInputEventSystem(transform);
            if (system == null || system.currentSelectedGameObject != gameObject)
            {
                return;
            }

            eventData.Use();
            if (authoredNavigation.mode == UnityEngine.UI.Navigation.Mode.None)
            {
                return;
            }

            Selectable neighbour;
            target.navigation = authoredNavigation;
            try
            {
                neighbour = FindNeighbour(eventData.moveDir);
            }
            finally
            {
                if (target != null)
                {
                    target.navigation = heldNavigation;
                }
            }

            if (neighbour != null && neighbour.transform.IsChildOf(itemView.transform))
            {
                ForwardNativeMove(eventData, neighbour);
            }
            else if (EditsNativeValue(eventData.moveDir) || !owner.UsesFocusDirection(eventData.moveDir))
            {
                var destinationView = neighbour == null ? null : neighbour.GetComponentInParent<View>(true);
                var eligible = destinationView != null && destinationView.IsInputEnabled &&
                    destinationView.CanReceiveSharedKeyboardInput();
                ForwardNativeMove(eventData, eligible ? neighbour : null);
            }
            else
            {
                // 不在同步原生回调中等待条目加载，完成和失败由同一异步列表入口观察。
                _ = ObserveMoveAsync(owner, eventData.moveDir);
            }
        }

        private Selectable FindNeighbour(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Left:
                    return target.FindSelectableOnLeft();
                case MoveDirection.Right:
                    return target.FindSelectableOnRight();
                case MoveDirection.Up:
                    return target.FindSelectableOnUp();
                case MoveDirection.Down:
                    return target.FindSelectableOnDown();
                default:
                    return null;
            }
        }

        private bool EditsNativeValue(MoveDirection direction)
        {
            var horizontal = direction == MoveDirection.Left || direction == MoveDirection.Right;
            if (target is Slider slider)
            {
                var sliderHorizontal = slider.direction == Slider.Direction.LeftToRight || slider.direction == Slider.Direction.RightToLeft;
                return horizontal == sliderHorizontal;
            }

            if (target is Scrollbar scrollbar)
            {
                var scrollbarHorizontal = scrollbar.direction == Scrollbar.Direction.LeftToRight || scrollbar.direction == Scrollbar.Direction.RightToLeft;
                return horizontal == scrollbarHorizontal;
            }

            return false;
        }

        private void ForwardNativeMove(AxisEventData eventData, Selectable destination)
        {
            var navigation = heldNavigation;
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    navigation.selectOnLeft = destination;
                    break;
                case MoveDirection.Right:
                    navigation.selectOnRight = destination;
                    break;
                case MoveDirection.Up:
                    navigation.selectOnUp = destination;
                    break;
                case MoveDirection.Down:
                    navigation.selectOnDown = destination;
                    break;
            }

            target.navigation = navigation;
            try
            {
                target.OnMove(eventData);
            }
            finally
            {
                if (target != null)
                {
                    target.navigation = heldNavigation;
                }
            }
        }

        private static async Task ObserveMoveAsync(VirtualListElement list, MoveDirection direction)
        {
            try
            {
                var result = await list.MoveFocusAsync(direction);
                if (result.Status == VirtualListRevealStatus.Failed && result.Error != null)
                {
                    UIErrors.Report(result.Error);
                }
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
