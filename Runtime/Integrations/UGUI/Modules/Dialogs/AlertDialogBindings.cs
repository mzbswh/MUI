using MUI.Unity;
using UnityEngine.UI;
using MUI.Dialogs;
using MUI.Pages;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>标准单按钮提示框的可选 uGUI 模板绑定；项目可替换布局或另建提示路由。</summary>
    [BindingFactory(typeof(AlertViewModel))]
    public static class AlertDialogBindings
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
            new BindingEntry(nameof(AlertViewModel.Acknowledge), "Acknowledge", typeof(NativeControl), nameof(Button.onClick), BindingMode.OneWay, BindingEntryKind.Command, "interactable") { ComponentType = typeof(Button) },
        });

        /// <summary>创建本次界面的绑定上下文；生命周期由界面实例管理。</summary>
        public static BindingContext<AlertViewModel> Create(IView view, AlertViewModel model) => new Context(view, model);

        /// <summary>显式注册绑定工厂；直接使用 CreateDefinition 时已提供绑定工厂。</summary>
        public static void Register() => BindingRegistry.Register<AlertViewModel>(Create, Manifest);

        private static BindingEntry TextEntry(string property, string name) => new BindingEntry(property, name, typeof(NativeControl), nameof(Text.text), BindingMode.OneWay) { ComponentType = typeof(Text) };

        private sealed class Context : BindingContext<AlertViewModel>
        {
            internal Context(IView view, AlertViewModel model) : base(view, model)
            {
            }

            protected override void BuildBindings(BindingBuilder<AlertViewModel> builder)
            {
                builder.Property<NativeControl, string>("Title", nameof(AlertViewModel.Title), model => model.Title, null, nameof(Text.text), null, (element, value) => ((Text)element.Target).text = value, componentType: typeof(Text));
                builder.Property<NativeControl, string>("Message", nameof(AlertViewModel.Message), model => model.Message, null, nameof(Text.text), null, (element, value) => ((Text)element.Target).text = value, componentType: typeof(Text));
                builder.Property<NativeControl, string>("AcknowledgeLabel", nameof(AlertViewModel.AcknowledgeLabel), model => model.AcknowledgeLabel, null, nameof(Text.text), null, (element, value) => ((Text)element.Target).text = value, componentType: typeof(Text));
                builder.Command<NativeControl>("Acknowledge", model => model.Acknowledge, (element, handler) => element.AddCommandListener(((Button)element.Target).onClick, handler), (element, handler) => element.RemoveCommandListener(((Button)element.Target).onClick, handler), (element, enabled) => ((Button)element.Target).interactable = enabled, eventName: "onClick", interactableProperty: "interactable", componentType: typeof(Button));
            }
        }
    }
}
