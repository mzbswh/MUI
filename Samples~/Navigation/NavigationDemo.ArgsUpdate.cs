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
                () => model, _ => presenter, binding,
                policy: new RoutePolicy(existingInstance: ExistingInstancePolicy.ReturnReady));
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
                Debug.Log($"MUI 参数准备失败保留页面：状态={preparation.Status}，标题={model.Title}");

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

            var failingPresenter = new ArgsUpdatePagePresenter();
            var failingRoute = new Route<PageViewModel, PageArgs, int>("demo.args-commit-failure", resource,
                () => new PageViewModel(), _ => failingPresenter, binding);
            var failingPage = await navigator.OpenAsync(failingRoute, new PageArgs("Initial", 10), cancellation.Token);
            if (!failingPage.IsSuccess)
            {
                throw new InvalidOperationException("Commit failure walkthrough could not open.", failingPage.Error);
            }
            try
            {
                var failed = await navigator.UpdateArgsAsync(failingPage.Handle.Identity, failingRoute,
                    new PageArgs("Commit failure", 40), cancellation.Token);
                await navigator.WaitForCleanupAsync(failingPage.Handle.Identity);
                Debug.Log($"MUI 参数提交异常故障关闭：状态={failed.Status}，视图故障={failed.ViewFaulted}，" +
                    $"候选释放数={failingPresenter.CandidatesDisposed}");
            }
            finally
            {
                await navigator.ForceCloseAsync(failingPage.Handle.Identity);
            }

            await DemonstrateArgsCleanupAsync(navigator, resource, binding);
        }
    }
}
