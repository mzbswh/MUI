using System;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>将屏幕空间根 Canvas 的直接子项适配到安全像素区域。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed partial class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField]
        private bool fitHorizontal = true;
        [SerializeField]
        private bool fitVertical = true;
        private RectTransform target;
        private Canvas rootCanvas;
        private DrivenRectTransformTracker tracker;
        private Rect? safeAreaOverride;
        private Rect previousSafeArea;
        private Rect previousViewport;
        private Rect previousKeyboardArea;
        private bool dirty = true;
        private bool applying;

        public event Action<Rect> Applied;

        /// <summary>应用轴设置及可选键盘避让后的实际屏幕像素区域。</summary>
        public Rect AppliedScreenRect
        {
            get; private set;
        }

        public void SetSafeAreaOverride(Rect screenPixelRect)
        {
            ValidateRect(screenPixelRect, nameof(screenPixelRect));
            safeAreaOverride = screenPixelRect;
            dirty = true;
        }

        public void ClearSafeAreaOverride()
        {
            safeAreaOverride = null;
            dirty = true;
        }

        private void OnEnable()
        {
            target = (RectTransform)transform;
            ResolveCanvas();
            tracker.Add(this, target, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
            dirty = true;
            Refresh();
        }

        private void OnDisable()
        {
            tracker.Clear();
        }

        private void OnTransformParentChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            ResolveCanvas();
            dirty = true;
        }

        private void ResolveCanvas()
        {
            rootCanvas = null;
            var parent = transform.parent;
            if (parent == null)
            {
                throw new InvalidOperationException("SafeAreaFitter requires a root Canvas parent.");
            }

            var canvas = parent.GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
            {
                throw new InvalidOperationException("Place SafeAreaFitter directly under a screen-space root Canvas.");
            }

            if (canvas.targetDisplay != 0)
            {
                throw new InvalidOperationException("Screen.safeArea applies only to the primary display.");
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null && canvas.worldCamera.targetTexture != null)
            {
                throw new InvalidOperationException("SafeAreaFitter does not map screen safe areas into render textures.");
            }

            rootCanvas = canvas;
        }

        private void LateUpdate() => Refresh();

        private void OnValidate()
        {
            dirty = true;
        }

        /// <summary>持续检查安全区、视口、键盘区域和布局配置，变化时同步更新，不创建异步任务。</summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled || applying || target == null || rootCanvas == null)
            {
                return;
            }

            if (rootCanvas.renderMode == RenderMode.WorldSpace || rootCanvas.targetDisplay != 0)
            {
                return;
            }

            if (rootCanvas.renderMode == RenderMode.ScreenSpaceCamera && rootCanvas.worldCamera != null && rootCanvas.worldCamera.targetTexture != null)
            {
                return;
            }

            var viewport = rootCanvas.pixelRect;
            var safe = safeAreaOverride ?? Screen.safeArea;
            if (viewport.width <= 0 || viewport.height <= 0)
            {
                return;
            }

            ValidateRect(viewport, nameof(viewport));
            ValidateRect(safe, nameof(safe));
            var keyboard = ReadKeyboardArea();
            if (!dirty && previousSafeArea == safe && previousViewport == viewport && previousKeyboardArea == keyboard)
            {
                return;
            }

            applying = true;
            try
            {
                dirty = false;
                previousSafeArea = safe;
                previousViewport = viewport;
                previousKeyboardArea = keyboard;
                var minX = Mathf.Clamp(safe.xMin, viewport.xMin, viewport.xMax);
                var maxX = Mathf.Clamp(safe.xMax, viewport.xMin, viewport.xMax);
                var minY = Mathf.Clamp(safe.yMin, viewport.yMin, viewport.yMax);
                var maxY = Mathf.Clamp(safe.yMax, viewport.yMin, viewport.yMax);
                var available = Rect.MinMaxRect(
                    fitHorizontal ? minX : viewport.xMin,
                    fitVertical ? minY : viewport.yMin,
                    fitHorizontal ? maxX : viewport.xMax,
                    fitVertical ? maxY : viewport.yMax);
                AppliedScreenRect = ExcludeKeyboard(available, keyboard);
                target.anchorMin = new Vector2(
                    (AppliedScreenRect.xMin - viewport.xMin) / viewport.width,
                    (AppliedScreenRect.yMin - viewport.yMin) / viewport.height);
                target.anchorMax = new Vector2(
                    (AppliedScreenRect.xMax - viewport.xMin) / viewport.width,
                    (AppliedScreenRect.yMax - viewport.yMin) / viewport.height);
                target.offsetMin = target.offsetMax = Vector2.zero;
                var handlers = Applied;
                if (handlers != null)
                {
                    foreach (Action<Rect> handler in handlers.GetInvocationList())
                    {
                        try
                        {
                            handler(AppliedScreenRect);
                        }
                        catch (Exception error)
                        {
                            UIErrors.Report(error);
                        }
                    }
                }
            }
            finally
            {
                applying = false;
            }
        }

        private static void ValidateRect(Rect rect, string name)
        {
            if (!Finite(rect.x) || !Finite(rect.y) || !Finite(rect.width) || !Finite(rect.height) || !Finite(rect.xMax) || !Finite(rect.yMax) || rect.width < 0 || rect.height < 0)
            {
                throw new ArgumentOutOfRangeException(name, "Screen rectangles must be finite with nonnegative size.");
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
