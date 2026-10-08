using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class ContextMenuController
    {
        [SerializeField, Tooltip("返回登记到的导航 View；留空使用最近的 View。嵌套菜单应指定所属顶层导航 View。")]
        private View backNavigationView = null;
        private LifetimeScope backSession;

        /// <summary>每次菜单显示独立登记，确保新打开的菜单优先消费返回，且不积累在页面生命周期中。</summary>
        private void BeginBackSession()
        {
            var target = backNavigationView == null ? view : backNavigationView;
            if (target == null)
            {
                // 独立菜单没有导航宿主，仍可通过 EventSystem 的局部取消关闭。
                return;
            }
            if (!transform.IsChildOf(target.transform))
            {
                throw new InvalidOperationException("菜单的返回目标必须是所属层级中的 View。");
            }

            var lifetime = new LifetimeScope();
            try
            {
                target.RegisterLocalBack(lifetime, () => session && CanRun() && HandleBack());
                backSession = lifetime;
            }
            catch
            {
                _ = ReleaseBackSessionAsync(lifetime);
                throw;
            }
        }

        /// <summary>在焦点恢复之前撤销返回资格，并观察登记资源的释放结果。</summary>
        private void EndBackSession()
        {
            var lifetime = backSession;
            backSession = null;
            if (lifetime != null)
            {
                _ = ReleaseBackSessionAsync(lifetime);
            }
        }

        private static async Task ReleaseBackSessionAsync(LifetimeScope lifetime)
        {
            try
            {
                await lifetime.DisposeAsync();
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
