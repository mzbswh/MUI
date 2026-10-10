using System;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateRebindAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var original = new RebindPageViewModel();
            var borrowed = new RebindPageViewModel { Title = "换绑后的页面" };
            var presenter = new RebindPagePresenter();
            var route = new Route<PageViewModel, PageArgs, int>("demo.rebind", resource,
                () => original, _ => presenter, binding, host.ResolvePolicy());
            var opened = await navigator.OpenAsync(route, new PageArgs("原页面", 10), cancellation.Token);
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("换绑示例无法打开页面。", opened.Error);
            }

            try
            {
                var applied = await navigator.RebindAsync(opened.Handle.Identity, route, borrowed, cancellation.Token);
                var before = presenter.Notifications;
                original.Title = "旧模型不再通知 Presenter";
                borrowed.Title = "新模型通知 Presenter";
                navigator.TryGetViewModel<PageViewModel>(opened.Handle.Identity, out var current);
                Debug.Log($"MUI 换绑成功：状态={applied.Status}，同句柄查询新模型={ReferenceEquals(current, borrowed)}，" +
                    $"打开次数={presenter.OpenCount}，通知增量={presenter.Notifications - before}，原模型释放数={original.ReleaseCount}");

                var failed = await navigator.RebindAsync(opened.Handle.Identity, route,
                    new PageViewModel { Title = "拒绝换绑" }, cancellation.Token);
                await navigator.WaitForCleanupAsync(opened.Handle.Identity);
                Debug.Log($"MUI 换绑提交故障：状态={failed.Status}，视图故障={failed.ViewFaulted}，页面={navigator.GetState(opened.Handle.Identity)}");
            }
            finally
            {
                await navigator.ForceCloseAsync(opened.Handle.Identity);
                await navigator.WaitForCleanupAsync(opened.Handle.Identity);
                Debug.Log($"MUI 借用模型关闭后释放数={borrowed.ReleaseCount}，由调用方随后释放");
                await borrowed.DisposeAsync();
            }

            await DemonstrateRebindCloseAsync(navigator, resource, binding);
        }

        private async Task DemonstrateRebindCloseAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var original = new RebindPageViewModel();
            var route = new Route<PageViewModel, PageArgs, int>("demo.rebind-close", resource,
                () => original, _ => new RebindPagePresenter(), binding, host.ResolvePolicy());
            var opened = await navigator.OpenAsync(route, new PageArgs("延迟释放页面", 10), cancellation.Token);
            if (!opened.IsSuccess)
            {
                original.AllowRelease.TrySetResult(true);
                throw new InvalidOperationException("换绑关闭示例无法打开页面。", opened.Error);
            }

            try
            {
                original.DelayRelease = true;
                var rebind = navigator.RebindAsync(opened.Handle.Identity, route,
                    new PageViewModel { Title = "提交后等待原模型释放" }).AsTask();
                // 实际释放入口发出信号后才请求关闭，不依赖延时猜测执行顺序。
                await Task.WhenAny(original.ReleaseStarted.Task, rebind);
                if (!original.ReleaseStarted.Task.IsCompleted)
                {
                    var failed = await rebind;
                    throw new InvalidOperationException($"示例换绑提前结束：{failed.Status}", failed.Error);
                }
                var close = navigator.ForceCloseAsync(opened.Handle.Identity).AsTask();
                Debug.Log($"MUI 换绑释放期间：换绑完成={rebind.IsCompleted}，关闭完成={close.IsCompleted}");
                original.AllowRelease.TrySetResult(true);
                Debug.Log($"MUI 换绑释放结束：换绑={(await rebind).Status}，关闭={(await close).Status}，释放数={original.ReleaseCount}");
            }
            finally
            {
                original.AllowRelease.TrySetResult(true);
                await navigator.ForceCloseAsync(opened.Handle.Identity);
            }
        }

        private async Task DemonstrateChildRebindAsync(ChildViewHandle<ThingItemViewModel, string> child,
            ThingItemPresenter presenter)
        {
            var previous = child.ViewModel;
            var next = new ThingItemViewModel { Label = "换绑道具 × 8" };
            var applied = await child.RebindAsync(next, cancellation.Token);
            Debug.Log($"MUI 子视图换绑：状态={applied.Status}，新模型={ReferenceEquals(child.ViewModel, next)}，" +
                $"打开次数={presenter.OpenCount}，文本={child.ViewModel.Label}");
            await child.RebindAsync(previous, cancellation.Token);
        }
    }
}
