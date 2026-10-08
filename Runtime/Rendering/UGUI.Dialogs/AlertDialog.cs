using MUI.Dialogs;
using MUI.Navigation;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>标准单按钮提示框的 UGUI 绑定与路由工厂；项目可替换布局或另建提示路由。</summary>
    [BindingFactory(typeof(AlertViewModel))]
    public static class AlertDialog
    {
        /// <summary>编辑器校验及显式注册使用的控件契约。</summary>
        public static BindingManifest Manifest
        {
            get;
        } = new BindingManifest(typeof(AlertViewModel), new[]
        {
            TextEntry(nameof(AlertViewModel.Title), "Title"),
            TextEntry(nameof(AlertViewModel.Message), "Message"),
            TextEntry(nameof(AlertViewModel.AcknowledgeLabel), "AcknowledgeLabel"),
            new BindingEntry(nameof(AlertViewModel.AcknowledgeLabel), "Acknowledge", typeof(ButtonElement), nameof(Element.AccessibilityLabel), BindingMode.OneWay),
            new BindingEntry(nameof(AlertViewModel.Acknowledge), "Acknowledge", typeof(ButtonElement), nameof(ButtonElement.Clicked), BindingMode.OneWay, BindingEntryKind.Command),
        });

        /// <summary>创建单实例模态路由，使用统一导航及命令生命周期。</summary>
        public static Route<AlertViewModel, AlertRequest, Unit> CreateRoute(
            ViewResource resource, string key = "mui.alert", int layer = 1000)
        {
            return new Route<AlertViewModel, AlertRequest, Unit>(
                key, resource,
                () => new AlertViewModel(),
                model => new AlertPresenter(),
                Create,
                new RoutePolicy(layer: layer, enterHistory: false, allowMultiple: true, maxInstances: 1,
                    coverage: CoveragePolicy.BlockInput, modal: true));
        }

        /// <summary>创建本次界面的绑定上下文；生命周期由界面实例管理。</summary>
        public static BindingContext<AlertViewModel> Create(IView view, AlertViewModel model) => new Context(view, model);

        /// <summary>显式注册绑定工厂；直接使用 CreateRoute 时已提供绑定工厂。</summary>
        public static void Register() => BindingRegistry.Register<AlertViewModel>(Create, Manifest);

        private static BindingEntry TextEntry(string property, string name) => new BindingEntry(property, name, typeof(TextElement), nameof(TextElement.Content), BindingMode.OneWay);

        private sealed class Context : BindingContext<AlertViewModel>
        {
            internal Context(IView view, AlertViewModel model) : base(view, model)
            {
            }

            protected override void BuildBindings(BindingBuilder<AlertViewModel> builder)
            {
                builder.Property<TextElement, string>("Title", nameof(AlertViewModel.Title), model => model.Title, null, nameof(TextElement.Content), null, (element, value) => element.Content = value);
                builder.Property<TextElement, string>("Message", nameof(AlertViewModel.Message), model => model.Message, null, nameof(TextElement.Content), null, (element, value) => element.Content = value);
                builder.Property<TextElement, string>("AcknowledgeLabel", nameof(AlertViewModel.AcknowledgeLabel), model => model.AcknowledgeLabel, null, nameof(TextElement.Content), null, (element, value) => element.Content = value);
                builder.Property<ButtonElement, string>("Acknowledge", nameof(AlertViewModel.AcknowledgeLabel), model => model.AcknowledgeLabel, null, nameof(Element.AccessibilityLabel), null, (element, value) => element.AccessibilityLabel = value);
                builder.Command<ButtonElement>("Acknowledge", model => model.Acknowledge, (element, handler) => element.Clicked += handler, (element, handler) => element.Clicked -= handler, (element, enabled) => element.Interactable = enabled);
            }
        }
    }
}
