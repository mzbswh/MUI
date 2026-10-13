using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>放在菜单 Button 旁，将 EventSystem 的取消事件转给所属菜单。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ContextMenuItemInput : MonoBehaviour, ICancelHandler
    {
        public void OnCancel(BaseEventData eventData)
        {
            if (!isActiveAndEnabled || eventData == null || eventData.used)
            {
                return;
            }

            var menu = GetComponentInParent<ContextMenuController>();
            var source = GetComponent<Button>();
            if (menu == null || !menu.CanHandleBack || source == null || !source.isActiveAndEnabled || !source.IsInteractable())
            {
                return;
            }

            var system = eventData.currentInputModule == null
                ? EventSystem.current
                : eventData.currentInputModule.GetComponent<EventSystem>();
            if (system == null || system.currentSelectedGameObject != gameObject)
            {
                return;
            }

            // 重复输入也标记为已用，不能因焦点已迁移而继续传给页面。
            eventData.Use();
            if (BackInputConsumption.TryConsume(system))
            {
                menu.HandleBack();
            }
        }
    }
}
