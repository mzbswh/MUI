using System.ComponentModel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>可选的预制背景射线区域，位于浮层内容子项之后。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AnchoredOverlayElement), typeof(Image))]
    public sealed class OverlayDismissArea : MonoBehaviour, IPointerDownHandler, ICanvasRaycastFilter
    {
        private AnchoredOverlayElement overlay;
        private Image surface;
        private View view;

        private void OnEnable()
        {
            overlay = GetComponent<AnchoredOverlayElement>();
            surface = GetComponent<Image>();
            view = GetComponentInParent<View>(true);
            overlay.PropertyChanged += OnOverlayChanged;
            if (view != null)
            {
                view.InputStateChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (overlay != null)
            {
                overlay.PropertyChanged -= OnOverlayChanged;
            }

            if (view != null)
            {
                view.InputStateChanged -= Refresh;
            }

            if (surface != null)
            {
                surface.raycastTarget = false;
                surface.enabled = false;
            }
        }

        private void LateUpdate() => Refresh();

        private bool CanIntercept() => isActiveAndEnabled && overlay != null && overlay.IsAlive && overlay.isActiveAndEnabled && overlay.Anchor != null && overlay.ContentRoot != null && overlay.ContentRoot.gameObject.activeInHierarchy && (view == null || view.IsInputEnabled);

        private void Refresh()
        {
            if (surface == null)
            {
                return;
            }

            var active = CanIntercept();
            surface.raycastTarget = active;
            surface.enabled = active;
        }

        private void OnOverlayChanged(object sender, PropertyChangedEventArgs args) => Refresh();

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            // 此过滤器也影响后代，不能排除内容矩形本身。
            // 禁用适配器不会禁用单独配置的子节点射线目标。
            return !isActiveAndEnabled || CanIntercept();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.used || !CanIntercept())
            {
                return;
            }

            // 背景持有本次按下事件；原生 EventSystem 会保持按下目标直至释放。
            // 不能将本次按下重新派发给正在关闭菜单下方的页面控件。
            eventData.Use();
            overlay.HandleOutsidePointer(eventData.position, eventData.pressEventCamera);
        }
    }
}
