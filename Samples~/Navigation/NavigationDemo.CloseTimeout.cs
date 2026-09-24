using System;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateCloseTimeoutAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var presenter = new CloseTimeoutPagePresenter();
            var route = new Route<PageViewModel, PageArgs, int>("demo.close-timeout", resource,
                () => new PageViewModel(), _ => presenter, binding,
                policy: new RoutePolicy(closeTimeout: TimeSpan.FromMilliseconds(50)));
            var opened = await navigator.OpenAsync(route, new PageArgs("关闭超时演示", 1), cancellation.Token);
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("无法打开关闭超时演示页面。", opened.Error);
            }

            try
            {
                var closed = await navigator.ForceCloseAsync(opened.Handle.Identity);
                var result = await opened.Handle.WaitForResultAsync(cancellation.Token);
                var physical = navigator.WaitForCleanupAsync(opened.Handle.Identity).AsTask();
                Debug.Log($"MUI 关闭超时：关闭={closed.Status}/{closed.Cleanup}，结果={result.Status}/{result.Cleanup}，" +
                    $"隔离数={navigator.PendingCleanupCount}，物理清理完成={physical.IsCompleted}，关闭回调完成={presenter.Closed}");

                var blockedRoute = new Route<PageViewModel, PageArgs, int>("demo.close-timeout.capacity", resource,
                    () => new PageViewModel(), _ => new PagePresenter(), binding);
                var blocked = await navigator.OpenAsync(blockedRoute, new PageArgs("隔离容量已满", 2), cancellation.Token);
                Debug.Log($"MUI 关闭隔离容量：新页面={blocked.Status}/{blocked.Rejection}");
                if (blocked.IsSuccess)
                {
                    await navigator.ForceCloseAsync(blocked.Handle.Identity);
                }

                presenter.AllowClose.TrySetResult(true);
                var released = await physical;
                Debug.Log($"MUI 迟到清理：结果={released.Status}/{released.Cleanup}，隔离数={navigator.PendingCleanupCount}，" +
                    $"清理令牌已取消={presenter.SawCleanupCancellation}，关闭回调完成={presenter.Closed}");
            }
            finally
            {
                presenter.AllowClose.TrySetResult(true);
                await navigator.ForceCloseAsync(opened.Handle.Identity);
                await navigator.WaitForCleanupAsync(opened.Handle.Identity);
            }
        }
    }
}
