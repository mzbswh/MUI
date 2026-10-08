using System;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>立即返回的关闭守卫也通过统一关闭入口执行；拒绝时不发布业务结果。</summary>
        private async Task DemonstrateImmediateGuardAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> bindings)
        {
            var presenter = new ImmediateGuardPresenter();
            var route = new Route<PageViewModel, PageArgs, int>("demo.immediate-guard", resource,
                () => new PageViewModel(), _ => presenter, bindings);
            var opened = await navigator.OpenAsync(route, new PageArgs("立即完成的关闭守卫", 10), cancellation.Token);
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("关闭守卫示例页面打开失败。", opened.Error);
            }

            try
            {
                var denied = await navigator.CloseAsync(opened.Handle);
                Debug.Log($"关闭守卫拒绝：{denied.Status}，结果已发布={opened.Handle.TryGetResult(out _)}");
                presenter.AllowClose();
                var closed = await navigator.CloseAsync(opened.Handle);
                var hasResult = opened.Handle.TryGetResult(out var result);
                Debug.Log($"关闭守卫允许：{closed.Status}，直接读取结果={hasResult}/{result.Status}");
            }
            finally
            {
                presenter.AllowClose();
                await navigator.CloseAsync(opened.Handle);
            }
        }

        private sealed class ImmediateGuardPresenter : PagePresenter, ICloseGuard
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

            public ValueTask<CloseDecision> CanCloseAsync(CloseContext context, System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<CloseDecision>(allowClose ? CloseDecision.Allow : CloseDecision.Deny);
            }
        }
    }
}
