using System;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateTicksAsync(Navigator navigator, ViewResource resource,
                    Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            // 演示流程临时持有时钟，不要同时运行 UIHost.Update。
            host.enabled = false;
            var frame = new FrameDemoPresenter();
            var slow = new SlowDemoPresenter();
            Route<PageViewModel, PageArgs, int> Route(string key, PagePresenter presenter, RoutePolicy policy)
                => new Route<PageViewModel, PageArgs, int>(key, resource, () => new PageViewModel(),
                    _ => presenter, binding, policy);
            var framePage = await navigator.OpenAsync(Route("tick.frame", frame, new RoutePolicy(tickPause: TickPausePolicy.Covered)), new PageArgs("Frame tick", 0));
            var slowPage = await navigator.OpenAsync(Route("tick.slow", slow, new RoutePolicy(maxTickCatchUp: 2)), new PageArgs("Low frequency", 0));
            try
            {
                if (!framePage.IsSuccess || !slowPage.IsSuccess)
                {
                    throw new InvalidOperationException("Tick pages failed to open.");
                }

                navigator.Tick(0.6f);
                Debug.Log($"MUI Tick initial: frame={frame.Calls}; low={slow.Calls}");
                var cover = await navigator.OpenAsync(Route("tick.cover", new PagePresenter(), new RoutePolicy(layer: 100, coverage: CoveragePolicy.BlockInput)), new PageArgs("Tick cover", 0));
                if (!cover.IsSuccess)
                {
                    throw new InvalidOperationException("Tick cover failed.", cover.Error);
                }

                navigator.Tick(0.2f);
                Debug.Log($"MUI Tick covered: frame={frame.Calls}; low={slow.Calls}");
                await navigator.CloseAsync(cover.Handle);
                navigator.Tick(0.2f);
                Debug.Log($"MUI Tick resumed: frame={frame.Calls}; low={slow.Calls}");
                navigator.Tick(2);
                Debug.Log($"MUI Tick catchup bounded: low={slow.Calls}");
                frame.CloseOnTick = true;
                navigator.Tick(0.1f);
                var countAtClose = frame.Calls;
                navigator.Tick(0.1f);
                Debug.Log($"MUI Tick closed source: noFurtherTicks={frame.Calls == countAtClose}");
            }
            finally
            {
                if (framePage.IsSuccess)
                {
                    await navigator.CloseAsync(framePage.Handle);
                }

                if (slowPage.IsSuccess)
                {
                    await navigator.CloseAsync(slowPage.Handle);
                }

                host.enabled = true;
            }
        }

        private async Task DemonstrateChildTicksAsync(View parent)
        {
            host.enabled = false;
            var item = parent.transform.Find("ThingItem").GetComponent<View>();
            var resource = new ViewResource("tick.child");
            var presenter = new ChildTickPresenter();
            var template = new ChildViewTemplate<ThingItemViewModel, Unit>(resource,
                () => new ThingItemViewModel(), ThingItemViewModelBindingFactory.Create, _ => presenter,
                pauseTickWhenHidden: true);
            ChildViewHandle<ThingItemViewModel, Unit> childView = null;
            try
            {
                childView = await parent.ChildViews.PrepareAsync(template, new BorrowedViewProvider(resource, item), Unit.Value);
                childView.Commit();
                host.Navigator.Tick(0.1f);
                var activeCalls = presenter.Calls;
                childView.SetLocalState(false, false);
                host.Navigator.Tick(0.1f);
                Debug.Log($"MUI Child tick hidden: active={activeCalls}; paused={presenter.Calls == activeCalls}");
                childView.SetLocalState(true, true);
                host.Navigator.Tick(0.1f);
                Debug.Log($"MUI Child tick resumed: calls={presenter.Calls}");
                presenter.CloseOnTick = true;
                host.Navigator.Tick(0.1f);
                var closedCalls = presenter.Calls;
                host.Navigator.Tick(0.1f);
                Debug.Log($"MUI Child tick closed: stopped={presenter.Calls == closedCalls}; parentHasTicks={parent.HasChildTicks}");
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

        private sealed class ChildTickPresenter : Presenter<ThingItemViewModel, Unit, Unit>, IViewTick
        {
            public int Calls
            {
                get; private set;
            }

            public bool CloseOnTick
            {
                get; set;
            }

            public void OnViewTick(float unscaledDeltaTime)
            {
                ViewModel.Label = "Tick " + ++Calls;
                if (CloseOnTick)
                {
                    Context.RequestClose();
                }
            }
        }

        private sealed class FrameDemoPresenter : PagePresenter, IViewTick
        {
            public int Calls
            {
                get; private set;
            }

            public bool CloseOnTick
            {
                get; set;
            }

            public void OnViewTick(float unscaledDeltaTime)
            {
                Calls++;
                if (CloseOnTick)
                {
                    Context.RequestClose();
                }
            }
        }

        private sealed class SlowDemoPresenter : PagePresenter, ILowFrequencyViewTick
        {
            public float TickInterval => 0.25f;

            public int Calls
            {
                get; private set;
            }

            public void OnLowFrequencyTick(float intervalSeconds)
            {
                Calls++;
            }
        }
    }
}
