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
        [SerializeField]
        private EventSystem inputEventSystem;
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

        /// <summary>项目输入动作所属的 EventSystem；未指定时使用当前 EventSystem。</summary>
        public EventSystem InputEventSystem
        {
            get => inputEventSystem;
            set
            {
                ClearRequest();
                inputEventSystem = value;
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
            if (system == null || (inputEventSystem != null && system != inputEventSystem) ||
                system.currentSelectedGameObject != gameObject)
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

            // 保留 Cancel 的实际来源；LateUpdate 时 EventSystem.current 可能已切换到其他系统。
            var validCancel = pending != null && system != null &&
                system.isActiveAndEnabled && system.currentSelectedGameObject == gameObject;
            if (!direct && !validCancel)
            {
                return;
            }

            var inputSystem = validCancel ? system : inputEventSystem != null ? inputEventSystem : host.GetInputEventSystem();
            var accepted = host.TryRequestBack(inputSystem);
            if (validCancel && (accepted || host.ShouldConsumeSharedCancel(inputSystem)))
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
