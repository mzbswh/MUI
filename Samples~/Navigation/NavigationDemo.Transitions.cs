using System;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task ShowEnterTransitionPreviewAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var route = new Route<PageViewModel, PageArgs, int>("demo.enter-preview", resource,
                () => new PageViewModel(), _ => new PagePresenter(), (view, model) =>
                {
                    var native = view as View;
                    if (native == null)
                    {
                        throw new InvalidOperationException("Transition preview requires a UGUI View.");
                    }
                    native.EnterDuration = 0.8f;
                    native.ExitDuration = 0.4f;
                    return binding(view, model);
                }, policy: new RoutePolicy(layer: 100, modal: true), preparation: PreparationMode.Synchronous);
            var opened = navigator.Open(route, new PageArgs("Entering — controls unlock when fading finishes", 10));
            if (!opened.Handle.IsValid)
            {
                throw new InvalidOperationException("Enter preview could not open: " + opened.Status, opened.Error);
            }
            try
            {
                Debug.Log("MUI Enter preview: " + opened.Readiness.Status);
                var readiness = await opened.Handle.WaitUntilReadyAsync(cancellation.Token);
                if (readiness.Status == ViewReadinessStatus.WaitCancelled)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                }

                if (!readiness.IsReady)
                {
                    return;
                }
                Debug.Log("MUI Enter preview ready; degraded=" + readiness.IsDegraded);
                await Task.Delay(1200, cancellation.Token);
                // 普通关闭等待淡出与清理；取消演示时由 finally 强制收尾。
                var closed = await navigator.CloseAsync(opened.Handle.Identity, cancellation.Token);
                Debug.Log("MUI Exit preview: " + closed.Status + "/" + closed.Cleanup);
            }
            finally
            {
                await navigator.ForceCloseAsync(opened.Handle.Identity);
            }
        }
    }
}
