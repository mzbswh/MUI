using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateCoverageAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding,
            ViewHandle lowerHandle, View lowerView)
        {
            var group = lowerView.GetComponent<CanvasGroup>();
            foreach (var policy in new[]
            {
                host.ResolvePolicy(overrides: new RoutePolicyOverrides { Coverage = CoveragePolicy.None, TakesFocus = false, Modal = false }),
                host.ResolvePolicy(overrides: new RoutePolicyOverrides { Coverage = CoveragePolicy.BlockInput, Modal = false }),
                host.ResolvePolicy("FullScreen"),
                host.ResolvePolicy("Popup")
            })
            {
                var route = new Route<PageViewModel, PageArgs, int>("demo.coverage." + policy.Coverage + "." + policy.Modal, resource,
                    () => new PageViewModel(), _ => new PagePresenter(), binding, policy);
                var opened = await navigator.OpenAsync(route, new PageArgs("Coverage " + policy.Coverage, 0));
                if (!opened.IsSuccess)
                {
                    throw new InvalidOperationException("Coverage page failed to open.", opened.Error);
                }

                Debug.Log($"MUI Coverage {policy.Coverage}/modal={policy.Modal}: lowerAlpha={group.alpha}; lowerInput={group.interactable}; lowerFocus={navigator.FocusedHandle == lowerHandle}");
                if (policy.Modal)
                {
                    await Task.Yield();
                    Canvas.ForceUpdateCanvases();
                    var hits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                    {
                        position = new Vector2(2, 2)
                    }, hits);
                    Debug.Log($"MUI Modal outside raycast: {(hits.Count == 0 ? "None" : hits[0].gameObject.name)}; screen={Screen.width}x{Screen.height}");
                }
                await navigator.CloseAsync(opened.Handle);
                Debug.Log($"MUI Coverage restored: alpha={group.alpha}; input={group.interactable}; focus={navigator.FocusedHandle == lowerHandle}");
            }
        }
    }
}
