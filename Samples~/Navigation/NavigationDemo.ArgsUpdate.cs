using System;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateArgsUpdateAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var presenter = new ArgsUpdatePagePresenter();
            var model = new PageViewModel();
            var route = new Route<PageViewModel, PageArgs, int>("demo.args-update", resource,
                () => model, _ => presenter, binding);
            var opened = await navigator.OpenAsync(route, new PageArgs("Initial", 10), cancellation.Token);
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("Argument update walkthrough could not open.", opened.Error);
            }

            try
            {
                var updated = new PageArgs("Updated", 20);
                var applied = await navigator.UpdateArgsAsync(opened.Handle.Identity, route, updated, cancellation.Token);
                var reused = await navigator.OpenAsync(route, updated, cancellation.Token);
                Debug.Log($"MUI 参数更新成功：状态={applied.Status}，同句柄={opened.Handle.Identity == reused.Handle.Identity}，" +
                    $"打开次数={presenter.OpenCount}，上下文标题={presenter.CurrentArgs.Title}，模型标题={model.Title}");

                var preparation = await navigator.UpdateArgsAsync(opened.Handle.Identity, route,
                    new PageArgs("Prepare failure", 30), cancellation.Token);
                var rollback = await navigator.UpdateArgsAsync(opened.Handle.Identity, route,
                    new PageArgs("Rollback", 40), cancellation.Token);
                Debug.Log($"MUI 参数更新失败恢复：准备={preparation.Status}，提交={rollback.Status}，" +
                    $"上下文标题={presenter.CurrentArgs.Title}，模型标题={model.Title}，选择={model.Selection}");

                var late = navigator.UpdateArgsAsync(opened.Handle.Identity, route, new PageArgs("Slow", 50)).AsTask();
                var close = navigator.ForceCloseAsync(opened.Handle.Identity).AsTask();
                presenter.SlowRelease.TrySetResult(true);
                var cancelled = await late;
                var closed = await close;
                Debug.Log($"MUI 参数更新期间关闭：更新={cancelled.Status}，关闭={closed.Status}，" +
                    $"候选释放数={presenter.CandidatesDisposed}，打开次数={presenter.OpenCount}");
            }
            finally
            {
                presenter.SlowRelease.TrySetResult(true);
                await navigator.ForceCloseAsync(opened.Handle.Identity);
            }

            await DemonstrateArgsCleanupAsync(navigator, resource, binding);
        }
    }
}
