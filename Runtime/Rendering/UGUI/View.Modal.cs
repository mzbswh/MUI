using System;
using System.Threading;
using System.Threading.Tasks;
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
        private bool modalBarrierEnabled;
        private PointerEventData modalClosingPointer;
        private BaseInputModule modalClosingModule;
        private PointerEventData.InputButton modalClosingButton;

        /// <summary>隐藏候选的屏障先参与原生 Canvas 排布；深度就绪前不显示也不消费输入。</summary>
        public async Task PrepareModalBarrierAsync(CancellationToken cancellationToken)
        {
            RequireAlive();
            cancellationToken.ThrowIfCancellationRequested();
            if (modalBarrierEnabled)
            {
                return;
            }

            var barrier = EnsureModalBarrier();
            var graphic = barrier.GetComponent<Image>();
            graphic.raycastTarget = false;
            graphic.color = Color.clear;
            // 透明预备节点仍须参与批次，避免因透明网格剔除而永远得不到可用的射线深度。
            graphic.canvasRenderer.cullTransparentMesh = false;
            barrier.SetActive(true);
            var deadline = Time.realtimeSinceStartup + 2f;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireAlive();
                if (barrier == null || barrier != modalBarrier || !barrier.activeInHierarchy)
                {
                    throw new InvalidOperationException("Modal barrier preparation was invalidated.");
                }

                if (graphic.depth >= 0 && !graphic.canvasRenderer.cull)
                {
                    return;
                }

                if (Time.realtimeSinceStartup >= deadline)
                {
                    throw new TimeoutException("Modal barrier did not receive a raycast depth within the layout time budget.");
                }

                // Canvas.ForceUpdateCanvases 只触发布局回调，不能保证本帧已分配原生深度。
                await Task.Yield();
            }
        }

        /// <summary>自定义 Pointer 回调在请求关闭模态页前登记本次输入。</summary>
        public void CaptureModalPointer(PointerEventData eventData)
        {
            RequireAlive();
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            if (modalBarrierEnabled && modalBarrier != null && modalBarrier.activeSelf && eventData.currentInputModule != null)
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
                        bool transferred;
                        try
                        {
                            // 覆盖隐藏仍是活动页面，应保留原屏障供恢复；关闭已先撤销宿主可见性，才移交关闭手势。
                            transferred = !hostVisible && HoldModalBarrier(barrier, modalClosingPointer, modalClosingModule, modalClosingButton);
                        }
                        catch (Exception failure)
                        {
                            if (ReferenceEquals(modalBarrier, barrier))
                            {
                                modalBarrier = null;
                            }

                            Exception cleanupFailure = null;
                            try
                            {
                                if (barrier != null)
                                {
                                    barrier.SetActive(false);
                                }
                            }
                            catch (Exception error)
                            {
                                cleanupFailure = error;
                            }

                            try
                            {
                                if (barrier != null)
                                {
                                    Destroy(barrier);
                                }
                            }
                            catch (Exception error)
                            {
                                cleanupFailure = cleanupFailure == null ? error :
                                    new AggregateException(cleanupFailure, error);
                            }

                            if (cleanupFailure != null)
                            {
                                throw new AggregateException("Modal barrier handoff and cleanup failed.", failure, cleanupFailure);
                            }

                            throw;
                        }

                        if (transferred)
                        {
                            if (ReferenceEquals(modalBarrier, barrier))
                            {
                                modalBarrier = null;
                            }
                        }
                        else
                        {
                            // 保留已取得深度的透明节点；上层 Hide 页面退出后，可立即恢复下层模态屏障。
                            var graphic = barrier.GetComponent<Image>();
                            if (graphic != null)
                            {
                                graphic.raycastTarget = false;
                                graphic.color = Color.clear;
                            }
                        }
                    }
                }
                finally
                {
                    modalBarrierEnabled = false;
                    modalClosingPointer = null;
                    modalClosingModule = null;
                }
                return;
            }

            var barrierObject = EnsureModalBarrier();
            var parent = transform.parent as RectTransform;

            // 导航器按从后到前排列页面；将本页的阻挡层放在其正下方。
            barrierObject.transform.SetAsLastSibling();
            transform.SetAsLastSibling();
            ModalPointerBarrier.RaiseHeldBarriers(parent);
            barrierObject.SetActive(true);
            var barrierGraphic = barrierObject.GetComponent<Image>();
            barrierGraphic.color = modalBarrierColor;
            barrierGraphic.raycastTarget = true;
            if (barrierGraphic.depth == -1 || barrierGraphic.canvasRenderer.cull)
            {
                throw new InvalidOperationException("Modal barrier must finish preparation before display is committed.");
            }
            modalBarrierEnabled = true;
        }

        private GameObject EnsureModalBarrier()
        {
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

            return modalBarrier;
        }

        private void ReleaseModalBarrier()
        {
            var barrier = modalBarrier;
            var pointer = modalClosingPointer;
            var module = modalClosingModule;
            var button = modalClosingButton;
            modalBarrier = null;
            modalBarrierEnabled = false;
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
