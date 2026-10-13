using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>在原生 ScrollRect 边界钳制前记录视口尺寸变化，不在布局回调中改写布局。</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class VirtualListViewportObserver : MonoBehaviour
    {
        private VirtualListElement owner;

        internal void Attach(VirtualListElement value) => owner = value;

        private void OnRectTransformDimensionsChange()
        {
            if (owner != null)
            {
                owner.CaptureViewportResize();
            }
        }
    }
}
