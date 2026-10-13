using System;
using System.Threading.Tasks;
using MUI.Pages;
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
                var button = view.GetControl<UnityEngine.UI.Button>(name);
                var input = button.GetComponent<UIBackInput>();
                if (input == null)
                {
                    input = button.gameObject.AddComponent<UIBackInput>();
                }

                input.Host = host;
                input.enabled = !automaticWalkthrough;
            }
        }

        private async Task DemonstrateBackAsync(PageManager pages, ViewResource resource,
                    Func<IView, PageViewModel, BindingContext<PageViewModel>> binding, PageHandle lower)
        {
            PageDefinition<PageViewModel, PageArgs, int> PageDefinition(string key, PagePolicy policy,
                Func<PageViewModel, Presenter<PageViewModel, PageArgs, int>> presenter = null)
                => new PageDefinition<PageViewModel, PageArgs, int>(key, resource, () => new PageViewModel(),
                    presenter ?? (_ => new PagePresenter()), binding, policy);
            var modal = await pages.OpenAsync(PageDefinition("back.modal", host.ResolvePolicy("Popup")), new PageArgs("Modal back handling", 0));
            if (!modal.IsSuccess)
            {
                throw new InvalidOperationException("Back modal failed.", modal.Error);
            }

            var closedModal = await pages.BackAsync();
            Debug.Log($"MUI Back modal: {closedModal.Status}; modal={pages.GetState(modal.Handle.Identity)}; lower={pages.GetState(lower)}");

            var blocker = await pages.OpenAsync(PageDefinition("back.block", host.ResolvePolicy(overrides: new PagePolicyOverrides { BackBehavior = BackBehavior.Block })), new PageArgs("Blocks back", 0));
            if (!blocker.IsSuccess)
            {
                throw new InvalidOperationException("Back blocker failed.", blocker.Error);
            }

            Debug.Log($"MUI Back blocked: {(await pages.BackAsync()).Status}; lower={pages.GetState(lower)}");
            await pages.CloseAsync(blocker.Handle);

            var baseLayer = host.ResolvePolicy().Layer;
            var target = await pages.OpenAsync(PageDefinition("back.target", host.ResolvePolicy(overrides: new PagePolicyOverrides { Layer = checked(baseLayer + 2) })), new PageArgs("Visually higher target", 0));
            var ignored = await pages.OpenAsync(PageDefinition("back.ignore", host.ResolvePolicy(overrides: new PagePolicyOverrides { Layer = checked(baseLayer + 3), BackBehavior = BackBehavior.Ignore })), new PageArgs("Ignore back", 0));
            var recent = await pages.OpenAsync(PageDefinition("back.recent", host.ResolvePolicy()), new PageArgs("Newer but visually lower", 0));
            if (!target.IsSuccess || !ignored.IsSuccess || !recent.IsSuccess)
            {
                throw new InvalidOperationException("Back order demo failed.");
            }

            var ordered = await pages.BackAsync();
            Debug.Log($"MUI Back order: {ordered.Status}; higher={pages.GetState(target.Handle.Identity)}; ignored={pages.GetState(ignored.Handle.Identity)}; recent={pages.GetState(recent.Handle.Identity)}");
            await pages.CloseAsync(ignored.Handle);
            await pages.CloseAsync(recent.Handle);

            var handler = new BackDemoPresenter(pages);
            var handled = await pages.OpenAsync(PageDefinition("back.handler", host.ResolvePolicy(overrides: new PagePolicyOverrides { BackBehavior = BackBehavior.HandleByPresenter }), _ => handler), new PageArgs("Local editing", 0));
            if (!handled.IsSuccess)
            {
                throw new InvalidOperationException("Back handler failed.", handled.Error);
            }

            Debug.Log($"MUI Back presenter first: {(await pages.BackAsync()).Status}; state={pages.GetState(handled.Handle.Identity)}");
            Debug.Log($"MUI Back presenter second: {(await pages.BackAsync()).Status}; lower={pages.GetState(lower)}");
        }

        private sealed class BackDemoPresenter : PagePresenter, IBackHandler
        {
            private readonly PageManager pages;
            private bool editing = true;

            public BackDemoPresenter(PageManager pages)
            {
                this.pages = pages;
            }

            public BackResponse HandleBack()
            {
                Debug.Log("MUI Back nested dispatch: " + pages.BackAsync().Result.Status);
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
