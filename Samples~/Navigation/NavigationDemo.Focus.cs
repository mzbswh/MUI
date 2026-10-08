using System;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateFocusAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding, View lower, ViewHandle lowerHandle)
        {
            var system = EventSystem.current;
            var lowerClose = lower.GetElement<ButtonElement>("Close").gameObject;
            system.SetSelectedGameObject(lowerClose);
            var route = new Route<PageViewModel, PageArgs, int>("focus.modal", resource,
                () => new PageViewModel(), _ => new PagePresenter(), binding,
                new RoutePolicy(modal: true));
            var opened = await navigator.OpenAsync(route, new PageArgs("Focus modal", 0));
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("Focus demo failed.", opened.Error);
            }

            var modal = FindFrontView();
            var confirm = modal.GetElement<ButtonElement>("Confirm").GetComponent<Button>();
            Debug.Log($"MUI Focus initial: selected={system.currentSelectedGameObject.name}; modalFocused={navigator.FocusedHandle == opened.Handle.Identity}");
            system.SetSelectedGameObject(lowerClose);
            navigator.Pump();
            Debug.Log($"MUI Focus constrained: inside={system.currentSelectedGameObject.transform.IsChildOf(modal.transform)}");
            confirm.interactable = false;
            navigator.Pump();
            Debug.Log("MUI Focus fallback: " + system.currentSelectedGameObject.name);
            using (modal.InputGate.Block("Focus disabled"))
            {
                Debug.Log($"MUI Focus blocked: none={navigator.FocusedHandle == default(ViewHandle)}; selectionCleared={system.currentSelectedGameObject == null}");
            }

            Debug.Log($"MUI Focus restored gate: modalFocused={navigator.FocusedHandle == opened.Handle.Identity}; selected={system.currentSelectedGameObject.name}");
            await navigator.CloseAsync(opened.Handle);
            Debug.Log($"MUI Focus lower restored: handle={navigator.FocusedHandle == lowerHandle}; previous={system.currentSelectedGameObject == lowerClose}");
            lower.Visible = false;
            Debug.Log($"MUI Focus locally hidden: none={navigator.FocusedHandle == default(ViewHandle)}");
            lower.Visible = true;
            Debug.Log($"MUI Focus locally shown: restored={navigator.FocusedHandle == lowerHandle}");
        }
    }
}
