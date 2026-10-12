using System;
using System.Threading.Tasks;
using MUI.Pages;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private async Task DemonstrateInputAsync(PageManager pages, ViewResource resource,
                    Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var presenter = new InputDemoPresenter();
            var route = new PageDefinition<PageViewModel, PageArgs, int>("input.demo", resource,
                () => new PageViewModel(), _ => presenter, binding,
                host.ResolvePolicy("Popup"));
            var opened = await pages.OpenAsync(route, new PageArgs("Two input blockers", 7));
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("Input demo failed.", opened.Error);
            }

            var view = FindFrontView();
            var input = view.InputGate;
            var inputSnapshot = view.CaptureInputSnapshot(maxBlockerReasons: 1);
            Debug.Log($"输入快照：View 资格={inputSnapshot.ViewInputEnabled}，限制={inputSnapshot.Restrictions}，局部阻挡={inputSnapshot.InputGate.BlockerCount}，已收录原因={inputSnapshot.InputGate.Reasons.Count}，截断={inputSnapshot.InputGate.IsTruncated}");
            var confirm = view.GetElement<ButtonElement>("Confirm").GetComponent<Button>();
            confirm.onClick.Invoke();
            Debug.Log($"MUI Input blocked: count={input.BlockerCount}; enabled={view.IsInputEnabled}; state={pages.GetState(opened.Handle.Identity)}; back={(await pages.BackAsync()).Status}");
            presenter.ExerciseTransientBlocks();
            Debug.Log($"MUI Input transient ownership: context={presenter.OwnedCount}; gate={input.BlockerCount}");
            presenter.ReleaseFirst();
            presenter.ReleaseFirst();
            confirm.onClick.Invoke();
            Debug.Log($"MUI Input partial release: count={input.BlockerCount}; enabled={view.IsInputEnabled}; state={pages.GetState(opened.Handle.Identity)}");
            presenter.ReleaseSecond();
            Debug.Log($"MUI Input all released: count={input.BlockerCount}; enabled={view.IsInputEnabled}");
            presenter.BlockUntilClose();
            await pages.CloseAsync(opened.Handle);
            Debug.Log($"MUI Input lifetime released: count={input.BlockerCount}; gateOpen={input.IsOpen}");
            var releasedGate = input.CaptureSnapshot();
            Debug.Log($"已释放门控快照：已释放={releasedGate.IsDisposed}，允许输入={releasedGate.IsOpen}，阻挡数={releasedGate.BlockerCount}");
        }

        private sealed class InputDemoPresenter : PagePresenter
        {
            private IDisposable first;
            private IDisposable second;

            public int OwnedCount => Context.InputBlockerCount;

            protected override void OnPrepareOpen(PageArgs args)
            {
                base.OnPrepareOpen(args);
                first = Context.BlockInput("Loading inventory");
                second = Context.BlockInput("Entering animation");
            }

            public void ExerciseTransientBlocks()
            {
                for (var i = 0; i < 1000; i++)
                {
                    using (Context.BlockInput("Transient operation"))
                    {
                    }
                }
            }

            public void ReleaseFirst() => first.Dispose();

            public void ReleaseSecond() => second.Dispose();

            public void BlockUntilClose() => Context.BlockInput("Owned until activation ends");
        }
    }
}
