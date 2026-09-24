using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        /// <summary>
        /// 在 Unity 主线程同步重建此 View 根节点内的原生布局，供显式即时测量使用。
        /// 不初始化绑定，不刷新祖先布局、其他 Canvas 或虚拟列表的屏外条目。
        /// 普通属性更新应沿用 Unity 的延迟布局，避免每次赋值都调用此方法。
        /// </summary>
        public void FlushLayout()
        {
            // 在存活检查和 transform 访问之前拒绝后台调用，避免触碰原生对象。
            ImmediateLayout.RequireMainThread();
            RequireAlive();
            var root = transform as RectTransform;
            if (root == null)
            {
                throw new InvalidOperationException("View 即时布局需要 RectTransform 根节点。");
            }

            ImmediateLayout.Rebuild(root);
            // 原生布局回调可能关闭或释放 View，不能将该状态报告为刷新成功。
            RequireAlive();
        }
    }
}
