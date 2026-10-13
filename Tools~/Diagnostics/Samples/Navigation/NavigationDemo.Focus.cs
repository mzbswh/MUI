using System;
using System.Threading.Tasks;
using MUI.Pages;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateFocusAsync(PageManager pages, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding, View lower, PageHandle lowerHandle)
        {
            var system = EventSystem.current;
            var lowerClose = lower.GetElement<ButtonElement>("Close").gameObject;
            system.SetSelectedGameObject(lowerClose);
            var route = new PageDefinition<PageViewModel, PageArgs, int>("focus.modal", resource,
                () => new PageViewModel(), _ => new PagePresenter(), binding,
                host.ResolvePolicy("Popup"));
            var opened = await pages.OpenAsync(route, new PageArgs("Focus modal", 0));
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("Focus demo failed.", opened.Error);
            }

            var modal = FindFrontView();
            var confirm = modal.GetElement<ButtonElement>("Confirm").GetComponent<Button>();
            Debug.Log($"MUI Focus initial: selected={system.currentSelectedGameObject.name}; modalFocused={pages.FocusedHandle == opened.Handle.Identity}");
            system.SetSelectedGameObject(lowerClose);
            pages.Pump();
            Debug.Log($"MUI Focus constrained: inside={system.currentSelectedGameObject.transform.IsChildOf(modal.transform)}");
            confirm.interactable = false;
            pages.Pump();
            Debug.Log("MUI Focus fallback: " + system.currentSelectedGameObject.name);
            using (modal.InputGate.Block("Focus disabled"))
            {
                Debug.Log($"MUI Focus blocked: none={pages.FocusedHandle == default(PageHandle)}; selectionCleared={system.currentSelectedGameObject == null}");
            }

            Debug.Log($"MUI Focus restored gate: modalFocused={pages.FocusedHandle == opened.Handle.Identity}; selected={system.currentSelectedGameObject.name}");
            await pages.CloseAsync(opened.Handle);
            Debug.Log($"MUI Focus lower restored: handle={pages.FocusedHandle == lowerHandle}; previous={system.currentSelectedGameObject == lowerClose}");
            lower.Visible = false;
            Debug.Log($"MUI Focus locally hidden: none={pages.FocusedHandle == default(PageHandle)}");
            lower.Visible = true;
            Debug.Log($"MUI Focus locally shown: restored={pages.FocusedHandle == lowerHandle}");
        }
    }
}
