using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        [SerializeField, Tooltip("可复用的模态遮罩样式。为空时使用本页遮罩颜色；自定义视觉只负责显示，输入屏障仍由框架提供。")]
        private ModalBackdropStyle modalBackdropStyle = null;
        private GameObject modalBackdropVisual;

        private Color ModalBackdropTint => modalBackdropStyle == null ? modalBarrierColor : modalBackdropStyle.Tint;

        private void CreateModalBackdropVisual()
        {
            if (modalBackdropStyle == null || modalBackdropStyle.VisualPrefab == null)
            {
                return;
            }
            var prefab = modalBackdropStyle.VisualPrefab;
            if (prefab.GetComponentInChildren<Canvas>(true) != null || prefab.GetComponentInChildren<Renderer>(true) != null ||
                prefab.GetComponentInChildren<Selectable>(true) != null || prefab.GetComponentInChildren<View>(true) != null)
            {
                throw new InvalidOperationException("模态遮罩视觉 Prefab 不能包含 Canvas、Renderer、Selectable 或 View；请使用 Image/RawImage 与项目材质。");
            }
            // 屏障此时为 inactive，项目脚本只能在框架正式显示遮罩时收到 OnEnable。
            var visual = Instantiate(prefab, modalBarrier.transform, false);
            modalBackdropVisual = visual.gameObject;
            visual.anchorMin = Vector2.zero;
            visual.anchorMax = Vector2.one;
            visual.offsetMin = Vector2.zero;
            visual.offsetMax = Vector2.zero;
            foreach (var graphic in visual.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
            var group = modalBackdropVisual.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = modalBackdropVisual.AddComponent<CanvasGroup>();
            }
            group.blocksRaycasts = false;
            group.interactable = false;
            group.ignoreParentGroups = false;
            modalBackdropVisual.SetActive(false);
        }

        private void SetModalBackdropVisible(bool visible)
        {
            if (modalBackdropVisual != null && modalBackdropVisual.activeSelf != visible)
            {
                modalBackdropVisual.SetActive(visible);
            }
        }
    }
}
