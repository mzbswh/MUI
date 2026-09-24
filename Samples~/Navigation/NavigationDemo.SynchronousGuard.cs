using System;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>默认异步宿主也可使用同步守卫；此演示不代表完整同步导航已接通。</summary>
        private async Task DemonstrateSynchronousGuardAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> bindings)
        {
            var presenter = new SynchronousGuardPresenter();
            var route = new Route<PageViewModel, PageArgs, int>("demo.synchronous-guard", resource,
                () => new PageViewModel(), _ => presenter, bindings);
            var opened = await navigator.OpenAsync(route, new PageArgs("同步关闭守卫", 10), cancellation.Token);
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("同步守卫示例页面打开失败。", opened.Error);
            }

            try
            {
                var synchronous = navigator.Close(opened.Handle);
                Debug.Log($"默认异步宿主的同步关闭准入：{synchronous.Status}，状态={navigator.GetState(opened.Handle.Identity)}");
                var denied = await navigator.CloseAsync(opened.Handle);
                Debug.Log($"同步守卫拒绝：{denied.Status}，结果已发布={opened.Handle.TryGetResult(out _)}");
                presenter.AllowClose();
                var closed = await navigator.CloseAsync(opened.Handle);
                var hasResult = opened.Handle.TryGetResult(out var result);
                Debug.Log($"同步守卫允许：{closed.Status}，直接读取结果={hasResult}/{result.Status}");
            }
            finally
            {
                presenter.AllowClose();
                await navigator.CloseAsync(opened.Handle);
            }
        }

        private sealed class SynchronousGuardPresenter : PagePresenter, ISynchronousCloseGuard
        {
            private bool allowClose;

            public long CloseVersion
            {
                get; private set;
            }

            public void AllowClose()
            {
                allowClose = true;
                ++CloseVersion;
            }

            public bool CanClose(CloseContext context) => allowClose;
        }
    }
}
