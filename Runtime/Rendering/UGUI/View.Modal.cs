using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        [SerializeField]
        private Color modalBarrierColor = new Color(0, 0, 0, 0.45f);
        private GameObject modalBarrier;

        public void SetModalBarrier(bool enabled)
        {
            RequireAlive();
            if (!enabled)
            {
                if (modalBarrier != null)
                {
                    modalBarrier.SetActive(false);
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
                modalBarrier = new GameObject("MUI Modal Barrier", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
            modalBarrier.SetActive(true);
        }

        private void ReleaseModalBarrier()
        {
            if (modalBarrier == null)
            {
                return;
            }

            modalBarrier.SetActive(false);
            Destroy(modalBarrier);
            modalBarrier = null;
        }
    }
}
