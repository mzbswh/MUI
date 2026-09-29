using System;
using System.Threading;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>记录 Unity 回调所在的线程，供 uGUI 宿主与布局入口校验。</summary>
    public static class UnityMainThread
    {
        private static int threadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Initialize()
        {
            Volatile.Write(ref threadId, Thread.CurrentThread.ManagedThreadId);
        }

        /// <summary>在访问 Unity 对象前拒绝非主线程或尚未初始化的调用。</summary>
        public static void Require()
        {
            var expected = Volatile.Read(ref threadId);
            if (expected == 0)
            {
                throw new InvalidOperationException("MUI.UGUI 尚未记录 Unity 主线程。");
            }

            if (Thread.CurrentThread.ManagedThreadId != expected)
            {
                throw new InvalidOperationException("MUI.UGUI 操作只能在 Unity 主线程调用。");
            }
        }
    }
}
