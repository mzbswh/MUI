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
        /// <summary>只为交互演示的页面按钮接入 Cancel；自动演示不接收人工返回输入。</summary>
        private void ConfigureBackInput(View view)
        {
            foreach (var name in new[] { "Confirm", "Close" })
            {
                var button = view.GetElement<ButtonElement>(name);
                var input = button.GetComponent<UIBackInput>();
                if (input == null)
                {
                    input = button.gameObject.AddComponent<UIBackInput>();
                }

                input.Host = host;
                input.enabled = !automaticWalkthrough;
            }
        }

        private async Task DemonstrateBackAsync(Navigator navigator, ViewResource resource,
                    Func<IView, PageViewModel, BindingContext<PageViewModel>> binding, ViewHandle lower)
        {
            Route<PageViewModel, PageArgs, int> Route(string key, RoutePolicy policy,
                Func<PageViewModel, Presenter<PageViewModel, PageArgs, int>> presenter = null)
                => new Route<PageViewModel, PageArgs, int>(key, resource, () => new PageViewModel(),
                    presenter ?? (_ => new PagePresenter()), binding, policy, PreparationMode.Synchronous);
            var modal = navigator.Open(Route("back.modal", new RoutePolicy(enterHistory: false, modal: true)), new PageArgs("Modal without history", 0));
            if (!modal.IsSuccess)
            {
                throw new InvalidOperationException("Back modal failed.", modal.Error);
            }

            var closedModal = await navigator.BackAsync();
            Debug.Log($"MUI Back modal: {closedModal.Status}; modal={navigator.GetState(modal.Handle.Identity)}; lower={navigator.GetState(lower)}");

            var blocker = navigator.Open(Route("back.block", new RoutePolicy(enterHistory: false, backBehavior: BackBehavior.Block)), new PageArgs("Blocks back", 0));
            if (!blocker.IsSuccess)
            {
                throw new InvalidOperationException("Back blocker failed.", blocker.Error);
            }

            Debug.Log($"MUI Back blocked: {(await navigator.BackAsync()).Status}; lower={navigator.GetState(lower)}");
            await navigator.CloseAsync(blocker.Handle);

            var target = navigator.Open(Route("back.target", new RoutePolicy(layer: 2)), new PageArgs("Visually higher target", 0));
            var ignored = navigator.Open(Route("back.ignore", new RoutePolicy(layer: 3, backBehavior: BackBehavior.Ignore)), new PageArgs("Ignore back", 0));
            var recent = navigator.Open(Route("back.recent", new RoutePolicy()), new PageArgs("Newer but visually lower", 0));
            if (!target.IsSuccess || !ignored.IsSuccess || !recent.IsSuccess)
            {
                throw new InvalidOperationException("Back order demo failed.");
            }

            var ordered = await navigator.BackAsync();
            Debug.Log($"MUI Back order: {ordered.Status}; higher={navigator.GetState(target.Handle.Identity)}; ignored={navigator.GetState(ignored.Handle.Identity)}; recent={navigator.GetState(recent.Handle.Identity)}");
            await navigator.CloseAsync(ignored.Handle);
            await navigator.CloseAsync(recent.Handle);

            var handler = new BackDemoPresenter(navigator);
            var handled = navigator.Open(Route("back.handler", new RoutePolicy(backBehavior: BackBehavior.HandleByPresenter), _ => handler), new PageArgs("Local editing", 0));
            if (!handled.IsSuccess)
            {
                throw new InvalidOperationException("Back handler failed.", handled.Error);
            }

            Debug.Log($"MUI Back presenter first: {(await navigator.BackAsync()).Status}; state={navigator.GetState(handled.Handle.Identity)}");
            Debug.Log($"MUI Back presenter second: {(await navigator.BackAsync()).Status}; lower={navigator.GetState(lower)}");
        }

        private sealed class BackDemoPresenter : PagePresenter, IBackHandler
        {
            private readonly Navigator navigator;
            private bool editing = true;

            public BackDemoPresenter(Navigator navigator)
            {
                this.navigator = navigator;
            }

            public BackResponse HandleBack()
            {
                Debug.Log("MUI Back nested dispatch: " + navigator.BackAsync().Result.Status);
                if (!editing)
                {
                    return BackResponse.Close;
                }

                editing = false;
                ViewModel.Title = "Local edit dismissed";
                return BackResponse.Handled;
            }
        }
    }
}
