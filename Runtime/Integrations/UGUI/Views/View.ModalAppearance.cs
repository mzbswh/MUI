using MUI.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        [SerializeField] private bool useGlobalModalBackdrop = false;

        [SerializeField, Tooltip("可复用的模态遮罩样式。为空时使用本页遮罩颜色；自定义视觉只负责显示，输入屏障仍由框架提供。")]
        private ModalBackdropStyle modalBackdropStyle = null;
        private GameObject modalBackdropVisual;

        private Color resolvedModalBackdropTint;
        private Color ModalBackdropTint => resolvedModalBackdropTint;

        private RectTransform ResolveModalBackdrop()
        {
            var settings = useGlobalModalBackdrop ? MUISettings.Global as UGUIHostSettings : null;
            var prefab = useGlobalModalBackdrop
                ? (settings == null ? null : settings.DefaultModalBackdropPrefab)
                : (modalBackdropStyle == null ? null : modalBackdropStyle.VisualPrefab);
            resolvedModalBackdropTint = useGlobalModalBackdrop
                ? (settings == null ? new Color(0, 0, 0, 0.45f) : settings.DefaultModalBackdropColor)
                : (modalBackdropStyle == null ? modalBarrierColor : modalBackdropStyle.Tint);
            ModalBackdropStyle.ValidateVisualPrefab(prefab);
            return prefab;
        }

        private void CreateModalBackdropVisual(RectTransform prefab)
        {
            if (prefab == null) return;
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
