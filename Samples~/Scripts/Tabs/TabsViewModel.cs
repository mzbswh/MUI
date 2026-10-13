using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Pages;
using MUI.Resources;
using MUI.Tabs;
using MUI.UGUI;
using MUI.Widgets;

namespace MUI.Examples
{
    [UIWidget("Tabs")]
    public partial class TabsViewModel : ViewModel, IAsyncDisposable
    {
        private readonly LifetimeScope resources = new LifetimeScope();
        internal TabContentController Controller;

        // Renderer integration stays here; the Prefab supplies all controls and content mounts.
        public static BindingContext<TabsViewModel> CreateBinding(IView target, TabsViewModel model)
        {
            var view = (View)target;
            var region = view.GetElement<AsyncContentElement>("ContentRegion");
            var bar = view.GetElement<TabBarElement>("TabBar");
            var provider = model.resources.Own(new LoadedPrefabViewProvider(view.transform, new UnityResourcesLoader()));
            var mounted = region.CreateProvider(provider);
            var attempts = 0;
            var inventory = new WidgetDefinition<TabContentViewModel, string>(new ViewResource("MUIExamples/TabContent"), () => new TabContentViewModel(), TabContentViewModelBindingFactory.Create, _ => new TabContentPresenter());
            var quests = new WidgetDefinition<TabContentViewModel, string>(new ViewResource("MUIExamples/TabContent"), () => new TabContentViewModel(), TabContentViewModelBindingFactory.Create, _ => new TabContentPresenter(() => ++attempts == 1));
            model.Controller = new TabContentController(view.Widgets, new[] { new TabContentDefinition("inventory", "背包", async (scope, token) => await scope.PrepareAsync(inventory, mounted, "背包", cancellationToken: token)), new TabContentDefinition("quests", "任务", async (scope, token) => await scope.PrepareAsync(quests, mounted, "任务", cancellationToken: token)), new TabContentDefinition("locked", "未解锁", async (scope, token) => await scope.PrepareAsync(inventory, mounted, "未解锁", cancellationToken: token), () => false) }, cacheOptions: new TabCacheOptions(TabContentRetention.CacheRecent, 2));
            region.Bind(model.Controller);
            bar.Bind(model.Controller);
            return TabsViewModelBindingFactory.Create(view, model);
        }

        public ValueTask DisposeAsync() => resources.DisposeAsync();
    }

    public sealed class TabsPresenter : Presenter<TabsViewModel, Unit, Unit>
    {
        protected override async void OnOpened()
        {
            try
            {
                await VM.Controller.SelectAsync("inventory", Context.Token);
            }
            catch (OperationCanceledException) when (Context.Token.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogException(error);
            }
        }
    }

    [UIWidget("TabContent")]
    public partial class TabContentViewModel : ViewModel
    {
        // 普通状态由关联 Prefab 生成。
    }

    public sealed class TabContentPresenter : Presenter<TabContentViewModel, string, Unit>, IAsyncOpenPreparation<string>
    {
        private readonly Func<bool> fail;

        public TabContentPresenter(Func<bool> fail = null) => this.fail = fail;

        public async ValueTask OnPrepareOpenAsync(string name, CancellationToken token)
        {
            await Task.Delay(500, token); // Stand-in for a project data request.
            if (fail != null && fail())
            {
                throw new InvalidOperationException("连接中断，请重试加载任务。");
            }

            Context.Apply(() =>
            {
                VM.Title = name;
                VM.Detail = name == "背包" ? "已准备好 24 件物品，随时可以出发。" : "有 3 个新任务等待完成。";
            });
        }
    }
}
