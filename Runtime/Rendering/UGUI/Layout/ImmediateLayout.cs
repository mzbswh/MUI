using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>主线程即时布局共用入口，阻止 View 与虚拟列表之间的嵌套重建。</summary>
    internal static class ImmediateLayout
    {
        private static bool rebuilding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            rebuilding = false;
        }

        internal static void Rebuild(RectTransform root)
        {
            UnityMainThread.Require();
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
