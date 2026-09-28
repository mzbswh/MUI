using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>主线程即时布局共用入口，阻止 View 与虚拟列表之间的嵌套重建。</summary>
    internal static class ImmediateLayout
    {
        private static int mainThreadId;
        private static bool rebuilding;

        // 只从 Unity 的主线程初始化回调记录身份，不能把首次调用者当成主线程。
        // 编辑器重载入口位于 Editor 程序集，运行会话由 Unity 的运行时回调重建记录。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void InitializeMainThread()
        {
            rebuilding = false;
            Volatile.Write(ref mainThreadId, Thread.CurrentThread.ManagedThreadId);
        }

        internal static void RequireMainThread()
        {
            var expectedThreadId = Volatile.Read(ref mainThreadId);
            if (expectedThreadId == 0)
            {
                throw new InvalidOperationException("即时布局尚未完成 Unity 主线程初始化。");
            }

            if (Thread.CurrentThread.ManagedThreadId != expectedThreadId)
            {
                throw new InvalidOperationException("即时布局只能在 Unity 主线程调用。");
            }
        }

        internal static void Rebuild(RectTransform root)
        {
            RequireMainThread();
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (rebuilding || CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                throw new InvalidOperationException("不能在布局或图形重建期间嵌套刷新布局。");
            }

            if (!root.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException("即时布局要求根节点在层级中激活。");
            }

            rebuilding = true;
            try
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                if (root == null || !root.gameObject.activeInHierarchy)
                {
                    throw new InvalidOperationException("布局回调期间根节点已销毁或停用，不能使用本次测量结果。");
                }
            }
            finally
            {
                rebuilding = false;
            }
        }
    }
}
