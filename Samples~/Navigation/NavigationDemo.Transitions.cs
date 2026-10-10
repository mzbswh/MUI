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
                }, policy: host.ResolvePolicy("Popup"));
            var opened = await navigator.OpenAsync(route, new PageArgs("Entering — controls unlock when fading finishes", 10), cancellation.Token);
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
                var entered = await opened.Handle.WaitForEnterAsync(cancellation.Token);
                Debug.Log("MUI Enter visual: " + entered.Status + "; degraded=" + entered.IsDegraded);
                await Task.Delay(1200, cancellation.Token);
                // 关闭只等待提交；需要确认淡出与资源归还时显式等待清理。
                var closed = await navigator.CloseAsync(opened.Handle.Identity, cancellation.Token);
                Debug.Log("MUI Exit preview: " + closed.Status + "/" + closed.Cleanup);
                var exited = await opened.Handle.WaitForExitAsync(cancellation.Token);
                Debug.Log("MUI Exit visual: " + exited.Status);
                await navigator.WaitForCleanupAsync(opened.Handle.Identity, cancellation.Token);
            }
            finally
            {
                await navigator.ForceCloseAsync(opened.Handle.Identity);
                await navigator.WaitForCleanupAsync(opened.Handle.Identity);
            }
        }
    }
}
