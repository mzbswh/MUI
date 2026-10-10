using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>只定义遮罩外观；输入屏障、排序、显隐和清理由框架管理。</summary>
    [CreateAssetMenu(menuName = "MUI/模态遮罩样式 (Modal Backdrop)", fileName = "ModalBackdropStyle")]
    public sealed class ModalBackdropStyle : ScriptableObject
    {
        [SerializeField] private Color tint = new Color(0, 0, 0, 0.45f);
        [SerializeField, Tooltip("可选纯视觉 Prefab，可使用 Image/RawImage 和项目模糊材质；不能包含独立 Canvas、Renderer、Selectable 或 View。隐藏时会停用实例。")]
        private RectTransform visualPrefab = null;

        public Color Tint => tint;

        public RectTransform VisualPrefab => visualPrefab;
    }
}
