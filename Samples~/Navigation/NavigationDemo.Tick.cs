using System;
using System.Threading.Tasks;
using MUI.Widgets;
using MUI.Pages;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateTicksAsync(PageManager pages, ViewResource resource,
                    Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            // 演示流程临时持有时钟，不要同时运行 UIHost.Update。
            host.enabled = false;
            var frame = new FrameDemoPresenter();
            var slow = new SlowDemoPresenter();
            PageDefinition<PageViewModel, PageArgs, int> PageDefinition(string key, PagePresenter presenter, PagePolicy policy)
                => new PageDefinition<PageViewModel, PageArgs, int>(key, resource, () => new PageViewModel(),
                    _ => presenter, binding, policy);
            var framePage = await pages.OpenAsync(PageDefinition("tick.frame", frame, host.ResolvePolicy(overrides: new PagePolicyOverrides { TickPause = TickPausePolicy.Covered })), new PageArgs("Frame tick", 0));
            var slowPage = await pages.OpenAsync(PageDefinition("tick.slow", slow, host.ResolvePolicy()), new PageArgs("Low frequency", 0));
            try
            {
                if (!framePage.IsSuccess || !slowPage.IsSuccess)
                {
                    throw new InvalidOperationException("Tick pages failed to open.");
                }

                pages.Tick(0.6f, 0.6f);
                Debug.Log($"MUI Tick initial: frame={frame.Calls}; low={slow.Calls}");
                var cover = await pages.OpenAsync(PageDefinition("tick.cover", new PagePresenter(), host.ResolvePolicy(layer: "Popup", overrides: new PagePolicyOverrides { Coverage = CoveragePolicy.BlockInput, Modal = false })), new PageArgs("Tick cover", 0));
                if (!cover.IsSuccess)
                {
                    throw new InvalidOperationException("Tick cover failed.", cover.Error);
                }

                pages.Tick(0.2f, 0.2f);
                Debug.Log($"MUI Tick covered: frame={frame.Calls}; low={slow.Calls}");
                await pages.CloseAsync(cover.Handle);
                pages.Tick(0.2f, 0.2f);
                Debug.Log($"MUI Tick resumed: frame={frame.Calls}; low={slow.Calls}");
                pages.Tick(2, 2);
                Debug.Log($"MUI Update coalesced: low={slow.Calls}");
                pages.Tick(0, 1f);
                Debug.Log($"MUI Update paused scale: frame={frame.Calls}; scaled={slow.Calls}");
                frame.CloseOnTick = true;
                pages.Tick(0.1f, 0.1f);
                var countAtClose = frame.Calls;
                pages.Tick(0.1f, 0.1f);
                Debug.Log($"MUI Tick closed source: noFurtherTicks={frame.Calls == countAtClose}");
            }
            finally
            {
                if (framePage.IsSuccess)
                {
                    await pages.CloseAsync(framePage.Handle);
                }

                if (slowPage.IsSuccess)
                {
                    await pages.CloseAsync(slowPage.Handle);
                }

                host.enabled = true;
            }
        }

        private async Task DemonstrateWidgetTicksAsync(View parent)
        {
            host.enabled = false;
            var item = parent.transform.Find("ThingItem").GetComponent<View>();
            var resource = new ViewResource("tick.child");
            var presenter = new WidgetTickPresenter();
            var template = new WidgetDefinition<ThingItemViewModel, Unit>(resource,
                () => new ThingItemViewModel(), ThingItemViewModelBindingFactory.Create, _ => presenter,
                pauseTickWhenHidden: true);
            WidgetHandle<ThingItemViewModel, Unit> childView = null;
            try
            {
                childView = await parent.Widgets.PrepareAsync(template, new BorrowedViewProvider(resource, item), Unit.Value);
                childView.Commit();
                host.Pages.Tick(0.1f, 0.1f);
                var activeCalls = presenter.Calls;
                childView.SetLocalState(false, false);
                host.Pages.Tick(0.1f, 0.1f);
                Debug.Log($"MUI Child tick hidden: active={activeCalls}; paused={presenter.Calls == activeCalls}");
                childView.SetLocalState(true, true);
                host.Pages.Tick(0.1f, 0.1f);
                Debug.Log($"MUI Child tick resumed: calls={presenter.Calls}");
                presenter.CloseOnTick = true;
                host.Pages.Tick(0.1f, 0.1f);
                var closedCalls = presenter.Calls;
                host.Pages.Tick(0.1f, 0.1f);
                Debug.Log($"MUI Child tick closed: stopped={presenter.Calls == closedCalls}; parentHasTicks={parent.HasWidgetTicks}");
            }
            finally
            {
                if (childView != null)
                {
                    await childView.DisposeAsync();
                }

                host.enabled = true;
            }
        }

        private sealed class WidgetTickPresenter : Presenter<ThingItemViewModel, Unit, Unit>
        {
            public int Calls
            {
                get; private set;
            }

            public bool CloseOnTick
            {
                get; set;
            }

            protected override UpdateOptions UpdateOptions => MUI.UpdateOptions.EveryFrame;

            protected override void OnUpdate(UpdateTime time)
            {
                ViewModel.Label = "Tick " + ++Calls;
                if (CloseOnTick)
                {
                    Context.RequestClose();
                }
            }
        }

        private sealed class FrameDemoPresenter : PagePresenter
        {
            public int Calls
            {
                get; private set;
            }

            public bool CloseOnTick
            {
                get; set;
            }

            protected override UpdateOptions UpdateOptions => MUI.UpdateOptions.EveryFrame;

            protected override void OnUpdate(UpdateTime time)
            {
                Calls++;
                if (CloseOnTick)
                {
                    Context.RequestClose();
                }
            }
        }

        private sealed class SlowDemoPresenter : PagePresenter
        {
            protected override UpdateOptions UpdateOptions => MUI.UpdateOptions.Every(0.25f, UpdateTimeMode.Scaled);

            public int Calls
            {
                get; private set;
            }

            protected override void OnUpdate(UpdateTime time)
            {
                Calls++;
            }
        }
    }
}
