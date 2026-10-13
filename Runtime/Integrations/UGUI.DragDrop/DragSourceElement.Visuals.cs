using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class DragSourceElement
    {
        [SerializeField]
        private Image dragIcon = null;
        [SerializeField]
        private RectTransform ghostRoot = null;
        private RectTransform ghost;
        private Camera ghostCamera;
        private long ghostGeneration;

        /// <summary>交互开始前配置预制或代码创建的拖拽影子。</summary>
        public void ConfigureGhost(Image icon, RectTransform root)
        {
            RequireAlive();
            if (interaction != null || starting)
            {
                throw new InvalidOperationException("Cannot reconfigure a drag ghost during an interaction.");
            }

            var previousIcon = dragIcon;
            var previousRoot = ghostRoot;
            dragIcon = icon;
            ghostRoot = root;
            try
            {
                ValidateGhost();
            }
            catch
            {
                dragIcon = previousIcon;
                ghostRoot = previousRoot;
                throw;
            }
        }

        private void ValidateGhost()
        {
            if (dragIcon == null && ghostRoot == null)
            {
                return;
            }

            if (dragIcon == null || ghostRoot == null)
            {
                throw new InvalidOperationException("Drag ghost requires both Drag Icon and Ghost Root.");
            }

            if (!dragIcon.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException("Drag Icon must belong to the drag source hierarchy.");
            }

            if (ghostRoot.IsChildOf(transform))
            {
                throw new InvalidOperationException("Ghost Root must be outside the drag source hierarchy.");
            }

            var sourceCanvas = GetComponentInParent<Canvas>();
            var targetCanvas = ghostRoot.GetComponentInParent<Canvas>();
            if (sourceCanvas == null || targetCanvas == null || sourceCanvas.rootCanvas != targetCanvas.rootCanvas)
            {
                throw new InvalidOperationException("Drag ghost and source must belong to the same root Canvas.");
            }
        }

        private void BeginGhost(PointerEventData data, long expected)
        {
            ValidateGhost();
            if (dragIcon == null)
            {
                return;
            }

            EndGhost();
            var canvas = ghostRoot.GetComponentInParent<Canvas>().rootCanvas;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null)
            {
                throw new InvalidOperationException("A camera/world-space drag ghost requires an explicit Canvas camera.");
            }

            ghostCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var node = new GameObject("MUI Drag Ghost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(LayoutElement));
            node.SetActive(false);
            ghost = (RectTransform)node.transform;
            ghostGeneration = expected;
            ghost.SetParent(ghostRoot, false);
            ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0.5f, 0.5f);
            var sourceRect = dragIcon.rectTransform;
            var width = ghostRoot.InverseTransformVector(sourceRect.TransformVector(new Vector3(sourceRect.rect.width, 0, 0))).magnitude;
            var height = ghostRoot.InverseTransformVector(sourceRect.TransformVector(new Vector3(0, sourceRect.rect.height, 0))).magnitude;
            if (float.IsNaN(width) || float.IsInfinity(width) || float.IsNaN(height) || float.IsInfinity(height))
            {
                throw new InvalidOperationException("Drag icon geometry must be finite.");
            }

            ghost.sizeDelta = new Vector2(width, height);
            node.GetComponent<LayoutElement>().ignoreLayout = true;
            var group = node.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var image = node.GetComponent<Image>();
            image.sprite = dragIcon.overrideSprite;
            image.color = dragIcon.color;
            image.preserveAspect = dragIcon.preserveAspect;
            image.raycastTarget = false;
            image.maskable = false;
            if (MoveGhost(data.position))
            {
                node.SetActive(true);
            }
        }

        private bool MoveGhost(Vector2 screenPosition)
        {
            if (ghost == null || ghostRoot == null)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(ghostRoot, screenPosition, ghostCamera, out var point))
            {
                return false;
            }

            ghost.localPosition = new Vector3(point.x, point.y, 0);
            return true;
        }

        private void EndGhost(long expected)
        {
            if (ghostGeneration == expected)
            {
                EndGhost();
            }
        }

        private void EndGhost()
        {
            var previous = ghost;
            ghost = null;
            ghostCamera = null;
            if (previous == null)
            {
                return;
            }

            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
        }
    }
}
