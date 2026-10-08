using System;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>展示清理失败不会撤回已提交参数，也不会被下一次成功更新从关闭诊断中抹去。</summary>
        private async Task DemonstrateArgsCleanupAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var route = new Route<PageViewModel, PageArgs, int>("demo.args-cleanup", resource,
                () => new PageViewModel(), _ => new ArgsUpdatePagePresenter(), binding);
            var opened = await navigator.OpenAsync(route, new PageArgs("清理错误演示", 1), cancellation.Token);
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("无法打开参数清理演示页面。", opened.Error);
            }

            try
            {
                var failedCleanup = await navigator.UpdateArgsAsync(opened.Handle.Identity, route,
                    new PageArgs("Cleanup failure", 2), cancellation.Token);
                var next = await navigator.UpdateArgsAsync(opened.Handle.Identity, route,
                    new PageArgs("随后成功更新", 3), cancellation.Token);
                var closed = await navigator.ForceCloseAsync(opened.Handle.Identity);
                Debug.Log($"MUI 参数清理错误保留：首次={failedCleanup.Status}/{failedCleanup.Cleanup}，" +
                    $"随后={next.Status}/{next.Cleanup}，关闭={closed.Status}/{closed.Cleanup}，有错误={closed.Error != null}");
            }
            finally
            {
                await navigator.ForceCloseAsync(opened.Handle.Identity);
            }
        }
    }
}
