using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    /// <summary>
    /// 把选中控件的 Cancel 转给宿主，或接收项目输入动作的无参数回调。
    /// 延后至 LateUpdate，让局部取消处理器先消费事件；不轮询按键，不创建任务。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIBackInput : MonoBehaviour, ICancelHandler
    {
        [SerializeField]
        private UIHost host;
        private bool requested;
        private BaseEventData cancelEvent;
        private EventSystem cancelSystem;
        private int cancelFrame = -1;
        private bool cancelConsumed;

        /// <summary>显式指定接收返回操作的宿主，避免多宿主场景隐式选择。</summary>
        public UIHost Host
        {
            get => host;
            set
            {
                ClearRequest();
                host = value;
            }
        }

        /// <summary>连接输入动作的按下阶段或平台回调；同帧请求合并，禁用时丢弃。</summary>
        public void RequestBack()
        {
            if (isActiveAndEnabled && host != null && host.isActiveAndEnabled)
            {
                requested = true;
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (!isActiveAndEnabled || host == null || !host.isActiveAndEnabled ||
                eventData == null)
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

            cancelConsumed |= eventData.used;
            cancelFrame = Time.frameCount;
            cancelEvent = eventData;
            cancelSystem = system;
        }

        private void LateUpdate()
        {
            var direct = requested;
            var pending = cancelEvent;
            var system = cancelSystem;
            var sameFrame = cancelFrame == Time.frameCount;
            var consumed = cancelConsumed;
            ClearRequest();
            if (host == null || !host.isActiveAndEnabled)
            {
                return;
            }

            // 同组件还收到全局动作回调时，局部消费仍优先，不能绕过已处理的 Cancel。
            if (pending != null && (!sameFrame || consumed || pending.used))
            {
                return;
            }

            // 不把焦点已迁移或其他 EventSystem 的事件转发给当前宿主。
            var validCancel = pending != null && system != null &&
                system == EventSystem.current && system.currentSelectedGameObject == gameObject;
            if (!direct && !validCancel)
            {
                return;
            }

            if (host.TryRequestBack() && validCancel)
            {
                pending.Use();
            }
        }

        private void OnDisable() => ClearRequest();

        private void ClearRequest()
        {
            requested = false;
            cancelEvent = null;
            cancelSystem = null;
            cancelFrame = -1;
            cancelConsumed = false;
        }
    }
}
