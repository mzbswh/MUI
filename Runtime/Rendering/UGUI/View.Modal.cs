using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        [SerializeField]
        private Color modalBarrierColor = new Color(0, 0, 0, 0.45f);
        private GameObject modalBarrier;
        private PointerEventData modalClosingPointer;
        private BaseInputModule modalClosingModule;
        private PointerEventData.InputButton modalClosingButton;

        /// <summary>自定义 Pointer 回调在请求关闭模态页前登记本次输入。</summary>
        public void CaptureModalPointer(PointerEventData eventData)
        {
            RequireAlive();
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            if (modalBarrier != null && modalBarrier.activeSelf && eventData.currentInputModule != null)
            {
                modalClosingPointer = eventData;
                modalClosingModule = eventData.currentInputModule;
                modalClosingButton = eventData.button;
            }
        }

        public void SetModalBarrier(bool enabled)
        {
            RequireAlive();
            if (!enabled)
            {
                var barrier = modalBarrier;
                try
                {
                    if (barrier != null)
                    {
                        if (HoldModalBarrier(barrier, modalClosingPointer, modalClosingModule, modalClosingButton))
                        {
                            if (ReferenceEquals(modalBarrier, barrier))
                            {
                                modalBarrier = null;
                            }
                        }
                        else
                        {
                            barrier.SetActive(false);
                        }
                    }
                }
                finally
                {
                    modalClosingPointer = null;
                    modalClosingModule = null;
                }
                return;
            }

            var parent = transform.parent as RectTransform;
            if (parent == null)
            {
                throw new InvalidOperationException("Modal View requires a RectTransform host region.");
            }

            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.overrideSorting)
                {
                    throw new InvalidOperationException("Modal content cannot bypass sibling order with Canvas.overrideSorting.");
                }
            }

            if (modalBarrier == null)
            {
                modalBarrier = new GameObject("MUI Modal Barrier", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ModalPointerBarrier));
                modalBarrier.SetActive(false);
                modalBarrier.transform.SetParent(parent, false);
                var rect = (RectTransform)modalBarrier.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var graphic = modalBarrier.GetComponent<Image>();
                graphic.color = modalBarrierColor;
                graphic.raycastTarget = true;
            }
            else if (modalBarrier.transform.parent != parent)
            {
                modalBarrier.transform.SetParent(parent, false);
            }

            // 导航器按从后到前排列页面；将本页的阻挡层放在其
            // 正下方，每个模态页面各自拥有独立阻挡层。
            modalBarrier.transform.SetAsLastSibling();
            transform.SetAsLastSibling();
            ModalPointerBarrier.RaiseHeldBarriers(parent);
            modalBarrier.SetActive(true);

            var barrierGraphic = modalBarrier.GetComponent<Image>();
            if (barrierGraphic == null)
            {
                throw new InvalidOperationException("Modal barrier has no Image.");
            }

            // uGUI 不会射线命中尚未由 Canvas 分配深度的 Graphic。
            if (barrierGraphic.depth == -1 && !CanvasUpdateRegistry.IsRebuildingLayout() &&
                !CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                Canvas.ForceUpdateCanvases();
            }

            if (barrierGraphic == null || barrierGraphic.depth == -1 ||
                barrierGraphic.canvasRenderer.cull || !barrierGraphic.raycastTarget)
            {
                throw new InvalidOperationException("Modal barrier is not ready for raycasts.");
            }
        }

        private void ReleaseModalBarrier()
        {
            var barrier = modalBarrier;
            var pointer = modalClosingPointer;
            var module = modalClosingModule;
            var button = modalClosingButton;
            modalBarrier = null;
            modalClosingPointer = null;
            modalClosingModule = null;
            if (barrier == null)
            {
                return;
            }

            var transferred = false;
            try
            {
                transferred = HoldModalBarrier(barrier, pointer, module, button);
            }
            finally
            {
                if (!transferred)
                {
                    try
                    {
                        barrier.SetActive(false);
                    }
                    finally
                    {
                        Destroy(barrier);
                    }
                }
            }
        }

        private static bool HoldModalBarrier(GameObject barrier, PointerEventData pointer,
            BaseInputModule module, PointerEventData.InputButton button)
        {
            if (barrier == null || !barrier.activeSelf || pointer == null)
            {
                return false;
            }

            var holder = barrier.GetComponent<ModalPointerBarrier>();
            if (holder == null)
            {
                return false;
            }

            holder.HoldUntilRelease(pointer, module, button);
            return true;
        }
    }
}
