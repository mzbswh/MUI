using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Dialogs;
using MUI.Pages;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Examples
{
    /// <summary>Project composition root. Layout and control references live in the scene and Prefabs.</summary>
    public sealed class ExampleWorkbench : MonoBehaviour
    {
        [SerializeField]
        private UIHost host = null;

        [SerializeField]
        private RectTransform dialogRoot = null;

        [SerializeField]
        private CanvasGroup navigation = null;

        [SerializeField]
        private Button[] lessonButtons = null;

        [SerializeField]
        private Text title = null;

        [SerializeField]
        private Text subtitle = null;

        [SerializeField]
        private Text footer = null;
        private readonly LifetimeScope lifetime = new LifetimeScope();

        [SerializeField]
        private ExampleLessonGuide lessonGuide = null;
        private PageHandle active;
        private FeedbackViewModel feedback;
        private bool changing;
        private Task shutdown;
        private Func<Task<PageHandle>>[] lessons;
        private PageDefinition<DetailViewModel, Unit, int> details;
        private PageDefinition<ConfirmationViewModel, CloseConfirmation, bool> confirmation;
        private PageDefinition<AlertViewModel, AlertRequest, Unit> alertDialog;
        private static readonly string[] Titles =
        {
            "设置你的偏好",
            "页面导航与返回结果",
            "子视图与绑定边界",
            "随数据变化的集合",
            "预先配置的固定槽位",
            "两千条数据，按需显示",
            "通过资源键加载内容",
            "异步页签与内容切换",
            "确认、取消与提示",
            "通知、进度与主题",
            "类型化拖放与异步提交"
        };
        private static readonly string[] Descriptions =
        {
            "属性绑定 · 表单与校验",
            "页面导航 · 类型化结果",
            "子视图 · 独立绑定边界",
            "集合列表 · 节点复用",
            "固定槽位 · 美术挂点",
            "虚拟列表 · 列表与网格",
            "资源绑定 · 凭证归还",
            "异步页签 · 加载与重试",
            "标准对话框 · 确认与取消",
            "反馈与主题 · 通知和任务状态",
            "类型化拖放 · 接受与拒绝"
        };

        private void Start()
        {
            Generated.ExampleBindings.Initialize();
            // The authored workbench Canvas is order 0; pages occupy a separate range above it.
            host.Initialize(new ExampleViewProvider(host.transform, dialogRoot), ownsProvider: true,
                renderOrder: new RenderOrderOptions(minimum: 100, maximum: 16000));
            ConfigureRoutes();
            for (var i = 0; i < lessonButtons.Length; ++i)
            {
                var lesson = i;
                lessonButtons[i].onClick.AddListener(() => SelectLesson(lesson));
            }

            SelectLesson(0);
        }

        private void Update()
        {
            if (feedback != null)
            {
                feedback.Advance(Time.unscaledDeltaTime);
            }
        }

        public async void SelectLesson(int index)
        {
            if (changing || lifetime.IsEnded || index < 0 || index >= Titles.Length)
            {
                return;
            }

            changing = true;
            navigation.interactable = false;
            try
            {
                if (active.IsValid)
                {
                    await host.Pages.CloseAsync(active, lifetime.Token);
                    var cleanup = await host.Pages.WaitForCleanupAsync(active, lifetime.Token);
                    if (cleanup.Error != null)
                    {
                        throw cleanup.Error;
                    }

                    if (cleanup.Cleanup != CleanupStatus.Complete)
                    {
                        throw new InvalidOperationException("上一页面尚未完成清理。");
                    }
                }

                active = default;
                feedback = null;
                lessonGuide.ShowLesson(index);
                title.text = Titles[index];
                subtitle.text = Descriptions[index];
                for (var i = 0; i < lessonButtons.Length; ++i)
                {
                    var colors = lessonButtons[i].colors;
                    colors.normalColor = i == index ? new Color(0.20f, 0.24f, 0.34f) : new Color(0.086f, 0.11f, 0.169f);
                    colors.disabledColor = colors.normalColor;
                    lessonButtons[i].colors = colors;
                }

                active = await lessons[index]();
                footer.text = "已就绪     /     " + Descriptions[index] + "     /     课程指南：演示、配置与代码";
            }
            catch (OperationCanceledException) when (lifetime.IsEnded)
            {
            }
            catch (Exception error)
            {
                if (footer != null)
                {
                    footer.text = "示例打开失败：" + error.Message;
                }

                Debug.LogException(error, this);
            }
            finally
            {
                changing = false;
                if (navigation != null && !lifetime.IsEnded)
                {
                    navigation.interactable = true;
                }
            }
        }

        // Definitions are immutable identities. Reuse them for every activation of this host.
        private void ConfigureRoutes()
        {
            var page = host.ResolvePolicy();
            var popup = host.ResolvePolicy("Popup");
            details = DetailViewModelDefinition.Create(() => new DetailViewModel(), policy: popup);
            confirmation = ConfirmationDialog.CreateDefinition(new ViewResource("MUIExamples/Confirmation"), policy: popup);
            alertDialog = AlertDialog.CreateDefinition(new ViewResource("MUIExamples/Alert"), policy: popup);
            lessons = new[]
            {
                Lesson(SettingsViewModelDefinition.Create(() => new SettingsViewModel(), policy: page)),
                Lesson(NavigationViewModelDefinition.Create(() => new NavigationViewModel(OpenDetailsAsync), policy: page)),
                Lesson(WidgetsViewModelDefinition.Create(() => new WidgetsViewModel(), policy: page)),
                Lesson(CollectionsViewModelDefinition.Create(() => new CollectionsViewModel(), policy: page)),
                Lesson(new PageDefinition<CollectionsViewModel, Unit, Unit>("examples.slots", new ViewResource("MUIExamples/FixedSlots"), () => new CollectionsViewModel(5), null, CollectionsViewModelBindingFactory.Create, policy: page)),
                Lesson(VirtualListViewModelDefinition.Create(() => new VirtualListViewModel(), policy: page)),
                Lesson(ResourcesViewModelDefinition.Create(() => new ResourcesViewModel(), policy: page)),
                Lesson(new PageDefinition<TabsViewModel, Unit, Unit>("examples.tabs", new ViewResource("MUIExamples/Tabs"), () => new TabsViewModel(), _ => new TabsPresenter(), TabsViewModel.CreateBinding, policy: page)),
                Lesson(DialogsViewModelDefinition.Create(() => new DialogsViewModel(ShowDialogAsync), policy: page)),
                Lesson(new PageDefinition<FeedbackViewModel, Unit, Unit>("examples.feedback", new ViewResource("MUIExamples/Feedback"), CreateFeedback, null, BindFeedback, policy: page)),
                Lesson(new PageDefinition<DragDropViewModel, Unit, Unit>("examples.drag", new ViewResource("MUIExamples/DragDrop"), () => new DragDropViewModel(), null, BindDragDrop, policy: page))
            };
        }

        private Func<Task<PageHandle>> Lesson<TModel>(PageDefinition<TModel, Unit, Unit> definition)
            where TModel : ViewModel => () => Open(definition);

        private FeedbackViewModel CreateFeedback()
        {
            feedback = new FeedbackViewModel();
            return feedback;
        }

        private static BindingContext<FeedbackViewModel> BindFeedback(IView view, FeedbackViewModel model)
        {
            var preview = ((View)view).GetElement<ImageElement>("PreviewCard").GetComponent<Image>();
            ThemeBindings.BindColor(model.Themes, model.Lifetime, preview, FeedbackViewModel.Surface);
            return FeedbackViewModelBindingFactory.Create(view, model);
        }

        private static BindingContext<DragDropViewModel> BindDragDrop(IView view, DragDropViewModel model)
        {
            ((View)view).GetElement<DragSourceElement>("Crate").Finished += model.OnFinished;
            return DragDropViewModelBindingFactory.Create(view, model);
        }

        private async Task<PageHandle> Open<TModel>(PageDefinition<TModel, Unit, Unit> definition)
            where TModel : ViewModel
        {
            var opened = await host.Pages.OpenAsync(definition, Unit.Value, lifetime.Token);
            if (!opened.IsSuccess)
            {
                throw new InvalidOperationException("打开失败：" + opened.Status, opened.Error);
            }

            return opened.Handle.Identity;
        }

        private async Task<string> OpenDetailsAsync(CancellationToken token)
        {
            navigation.interactable = false;
            try
            {
                var opened = await host.Pages.OpenAsync(details, Unit.Value, token, owner: PageOwner.Host);
                if (!opened.IsSuccess)
                {
                    throw new InvalidOperationException("详情页打开失败。", opened.Error);
                }

                var result = await opened.Handle.WaitForResultAsync(token);
                await WaitForCleanup(opened.Handle.Identity, token);
                return result.IsCompleted ? "已完成，返回值：" + result.Value : "已返回，未提交业务结果";
            }
            finally
            {
                if (navigation != null && !lifetime.IsEnded)
                {
                    navigation.interactable = true;
                }
            }
        }

        private async Task<string> ShowDialogAsync(bool alert, CancellationToken token)
        {
            navigation.interactable = false;
            try
            {
                if (alert)
                {
                    var opened = await host.Pages.OpenAsync(alertDialog, new AlertRequest("设置已更新", "你的探索偏好已更新，可以继续操作。", "知道了"), token, owner: PageOwner.Host);
                    if (!opened.IsSuccess)
                    {
                        throw new InvalidOperationException("提示框打开失败。", opened.Error);
                    }

                    var result = await opened.Handle.WaitForResultAsync(token);
                    await WaitForCleanup(opened.Handle.Identity, token);
                    return result.IsCompleted ? "提示已确认" : "提示已关闭";
                }

                var offered = await host.Pages.OpenAsync(confirmation, new CloseConfirmation("开始新的探索？", "当前物资将用于本次探索。请确认准备完成后再继续。", "开始探索", "暂不开始"), token, owner: PageOwner.Host);
                if (!offered.IsSuccess)
                {
                    throw new InvalidOperationException("确认框打开失败。", offered.Error);
                }

                var decision = await offered.Handle.WaitForResultAsync(token);
                await WaitForCleanup(offered.Handle.Identity, token);
                return decision.IsCompleted ? (decision.Value ? "已确认：探索已开始" : "已取消：未更改数据") : "已关闭：未提交决定";
            }
            finally
            {
                if (navigation != null && !lifetime.IsEnded)
                {
                    navigation.interactable = true;
                }
            }
        }

        private async Task WaitForCleanup(PageHandle handle, CancellationToken token)
        {
            var cleanup = await host.Pages.WaitForCleanupAsync(handle, token);
            if (cleanup.Error != null)
            {
                throw cleanup.Error;
            }

            if (cleanup.Cleanup != CleanupStatus.Complete)
            {
                throw new InvalidOperationException("页面尚未完成清理。");
            }
        }

        public Task ShutdownAsync() => shutdown ?? (shutdown = ShutdownCoreAsync());

        private async Task ShutdownCoreAsync()
        {
            lifetime.Cancel();
            feedback = null;
            await lifetime.DisposeAsync();
            if (host != null)
            {
                await host.ShutdownAsync();
            }
        }

        private async void OnDestroy()
        {
            try
            {
                await ShutdownAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
