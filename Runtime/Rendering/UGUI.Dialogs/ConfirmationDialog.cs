using MUI.Dialogs;
using MUI.Navigation;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>标准确认框的 UGUI 绑定与路由工厂；项目可替换布局或另建确认路由。</summary>
    [BindingFactory(typeof(ConfirmationViewModel))]
    public static class ConfirmationDialog
    {
        /// <summary>编辑器校验及显式注册使用的控件契约。</summary>
        public static BindingManifest Manifest
        {
            get;
        } = new BindingManifest(typeof(ConfirmationViewModel), new[]
        {
            TextEntry(nameof(ConfirmationViewModel.Title), "Title"),
            TextEntry(nameof(ConfirmationViewModel.Message), "Message"),
            TextEntry(nameof(ConfirmationViewModel.ConfirmLabel), "ConfirmLabel"),
            TextEntry(nameof(ConfirmationViewModel.CancelLabel), "CancelLabel"),
            new BindingEntry(nameof(ConfirmationViewModel.ConfirmLabel), "Confirm", typeof(ButtonElement), nameof(Element.AccessibilityLabel), BindingMode.OneWay),
            new BindingEntry(nameof(ConfirmationViewModel.CancelLabel), "Cancel", typeof(ButtonElement), nameof(Element.AccessibilityLabel), BindingMode.OneWay),
            new BindingEntry(nameof(ConfirmationViewModel.Confirm), "Confirm", typeof(ButtonElement), nameof(ButtonElement.Clicked), BindingMode.OneWay, BindingEntryKind.Command),
            new BindingEntry(nameof(ConfirmationViewModel.Cancel), "Cancel", typeof(ButtonElement), nameof(ButtonElement.Clicked), BindingMode.OneWay, BindingEntryKind.Command)
        });

        /// <summary>默认创建单实例模态路由；可显式传入已解析的项目策略。</summary>
        public static Route<ConfirmationViewModel, CloseConfirmation, bool> CreateRoute(
            ViewResource resource, string key = "mui.confirmation", int layer = 1000, RoutePolicy policy = null)
        {
            return new Route<ConfirmationViewModel, CloseConfirmation, bool>(
                key, resource,
                () => new ConfirmationViewModel(),
                model => new ConfirmationPresenter(),
                Create,
                policy ?? new RoutePolicy(layer: layer, allowMultiple: true, maxInstances: 1,
                    coverage: CoveragePolicy.BlockInput, modal: true, pageRole: PageRole.Independent));
        }

        /// <summary>创建本次界面的绑定上下文；生命周期由界面实例管理。</summary>
        public static BindingContext<ConfirmationViewModel> Create(IView view, ConfirmationViewModel model) => new Context(view, model);

        /// <summary>显式注册绑定工厂；直接使用 CreateRoute 时已提供绑定工厂。</summary>
        public static void Register() => BindingRegistry.Register<ConfirmationViewModel>(Create, Manifest);

        private static BindingEntry TextEntry(string property, string name) => new BindingEntry(property, name, typeof(TextElement), nameof(TextElement.Content), BindingMode.OneWay);

        private sealed class Context : BindingContext<ConfirmationViewModel>
        {
            internal Context(IView view, ConfirmationViewModel model) : base(view, model)
            {
            }

            protected override void BuildBindings(BindingBuilder<ConfirmationViewModel> builder)
            {
                builder.Property<TextElement, string>("Title", nameof(ConfirmationViewModel.Title), model => model.Title, null, nameof(TextElement.Content), null, (element, value) => element.Content = value);
                builder.Property<TextElement, string>("Message", nameof(ConfirmationViewModel.Message), model => model.Message, null, nameof(TextElement.Content), null, (element, value) => element.Content = value);
                builder.Property<TextElement, string>("ConfirmLabel", nameof(ConfirmationViewModel.ConfirmLabel), model => model.ConfirmLabel, null, nameof(TextElement.Content), null, (element, value) => element.Content = value);
                builder.Property<TextElement, string>("CancelLabel", nameof(ConfirmationViewModel.CancelLabel), model => model.CancelLabel, null, nameof(TextElement.Content), null, (element, value) => element.Content = value);
                builder.Property<ButtonElement, string>("Confirm", nameof(ConfirmationViewModel.ConfirmLabel), model => model.ConfirmLabel, null, nameof(Element.AccessibilityLabel), null, (element, value) => element.AccessibilityLabel = value);
                builder.Property<ButtonElement, string>("Cancel", nameof(ConfirmationViewModel.CancelLabel), model => model.CancelLabel, null, nameof(Element.AccessibilityLabel), null, (element, value) => element.AccessibilityLabel = value);
                builder.Command<ButtonElement>("Confirm", model => model.Confirm, (element, handler) => element.Clicked += handler, (element, handler) => element.Clicked -= handler, (element, enabled) => element.Interactable = enabled);
                builder.Command<ButtonElement>("Cancel", model => model.Cancel, (element, handler) => element.Clicked += handler, (element, handler) => element.Clicked -= handler, (element, enabled) => element.Interactable = enabled);
            }
        }
    }
}
