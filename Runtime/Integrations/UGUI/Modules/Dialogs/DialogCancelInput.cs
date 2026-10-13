using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>
    /// 将当前控件收到的键盘或手柄取消事件转给同一界面的取消按钮。
    /// 原生输入模块将取消事件发送给选中对象，因此需要挂在各个可选中的控件上。
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Selectable))]
    public sealed class DialogCancelInput : MonoBehaviour, ICancelHandler
    {
        [SerializeField]
        private Button cancelButton;

        /// <summary>取消操作的目标按钮；必须与本控件属于同一个 View。</summary>
        public Button CancelButton
        {
            get => cancelButton;
            set => cancelButton = value;
        }

        /// <summary>沿用按钮的绑定命令与输入门控，不直接关闭页面或绕过关闭守卫。</summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (eventData == null || eventData.used || !isActiveAndEnabled || cancelButton == null)
            {
                return;
            }

            var source = GetComponent<Selectable>();
            if (source == null || !source.isActiveAndEnabled || !source.IsInteractable() ||
                !cancelButton.isActiveAndEnabled || !cancelButton.IsInteractable())
            {
                return;
            }

            var owner = GetComponentInParent<View>(true);
            var targetOwner = cancelButton.GetComponentInParent<View>(true);
            if (owner == null || owner != targetOwner || !owner.IsInputEnabled)
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

            // 先消费事件，避免按钮回调引发焦点迁移后，同一事件再次触发其他操作。
            eventData.Use();
            if (BackInputConsumption.TryConsume(system))
            {
                cancelButton.onClick.Invoke();
            }
        }
    }
}
