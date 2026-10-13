using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>只定义遮罩外观；输入屏障、排序、显隐和清理由框架管理。</summary>
    [CreateAssetMenu(menuName = "MUI/模态遮罩样式 (Modal Backdrop)", fileName = "ModalBackdropStyle")]
    public sealed class ModalBackdropStyle : ScriptableObject
    {
        [SerializeField] private Color tint = new Color(0, 0, 0, 0.45f);
        [SerializeField, Tooltip("可选纯视觉 Prefab，可使用 Image/RawImage 和项目模糊材质；不能包含独立 Canvas、Renderer、Selectable 或 View。隐藏时会停用实例。")]
        private RectTransform visualPrefab = null;

        internal static void ValidateVisualPrefab(RectTransform prefab)
        {
            if (prefab == null) return;
            if (prefab.GetComponentInChildren<Canvas>(true) != null || prefab.GetComponentInChildren<Renderer>(true) != null ||
                prefab.GetComponentInChildren<Selectable>(true) != null || prefab.GetComponentInChildren<View>(true) != null)
                throw new InvalidOperationException("模态遮罩视觉 Prefab 不能包含 Canvas、Renderer、Selectable 或 View；请使用 Image/RawImage 与项目材质。");
        }

        public Color Tint => tint;

        public RectTransform VisualPrefab => visualPrefab;
    }
}
