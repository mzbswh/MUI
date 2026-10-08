using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    /// <summary>观察原生 ScrollRect 输入，并在阅读位置补偿后重设原生拖动基准。</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class VirtualListScrollInput : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler
    {
        private VirtualListElement owner;
        private int? dragPointer;
        private PointerEventData dragEvent;

        internal bool IsDragging => dragPointer.HasValue;

        internal void Attach(VirtualListElement value) => owner = value;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || owner == null || !owner.CanInterruptScroll)
            {
                return;
            }

            dragPointer = eventData.pointerId;
            dragEvent = eventData;
            owner.InterruptScroll();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragPointer == eventData.pointerId)
            {
                dragPointer = null;
                dragEvent = null;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragPointer == eventData.pointerId)
            {
                dragEvent = eventData;
            }
        }

        internal void RebaseDrag(UnityEngine.UI.ScrollRect scroll)
        {
            if (dragEvent != null && dragPointer.HasValue && scroll != null && scroll.isActiveAndEnabled)
            {
                scroll.OnBeginDrag(dragEvent);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData.scrollDelta != Vector2.zero && owner != null && owner.CanInterruptScroll)
            {
                owner.InterruptScroll();
            }
        }

        private void LateUpdate()
        {
            // 输入门控可能结束原生拖动而不派发业务 EndDrag，不能保留过期拖动状态。
            if (dragPointer.HasValue && (owner == null || !owner.CanInterruptScroll))
            {
                dragPointer = null;
                dragEvent = null;
            }
        }

        private void OnDisable()
        {
            dragPointer = null;
            dragEvent = null;
            if (owner != null)
            {
                owner.InterruptScroll();
            }
        }
    }
}
