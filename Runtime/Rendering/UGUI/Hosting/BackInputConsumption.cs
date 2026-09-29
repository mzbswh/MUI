using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    /// <summary>
    /// 合并同一 EventSystem 同帧的返回输入，防止局部 Cancel 和宿主 Back 重复派发。
    /// 仅用于输入适配；直接调用 Navigator 不受影响。所有入口须在 Unity 主线程调用。
    /// </summary>
    public static class BackInputConsumption
    {
        private static ConditionalWeakTable<EventSystem, FrameState> systems = new ConditionalWeakTable<EventSystem, FrameState>();
        private static FrameState withoutSystem = new FrameState();

        /// <summary>在确认本入口能够处理后、执行外部回调前消费；false 表示本帧已经有入口接纳。</summary>
        public static bool TryConsume(EventSystem system = null)
        {
            UnityMainThread.Require();
            if (system == null)
            {
                system = EventSystem.current;
            }

            var state = system == null ? withoutSystem : systems.GetValue(system, _ => new FrameState());
            if (state.Frame == Time.frameCount)
            {
                return false;
            }

            state.Frame = Time.frameCount;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // 禁用域重载时也不能把上一次运行的帧标记带入新会话。
            systems = new ConditionalWeakTable<EventSystem, FrameState>();
            withoutSystem = new FrameState();
        }

        private sealed class FrameState
        {
            internal int Frame = -1;
        }
    }
}
