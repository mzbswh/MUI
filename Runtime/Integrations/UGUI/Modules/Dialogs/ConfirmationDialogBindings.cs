using MUI.Unity;
using UnityEngine.UI;
using MUI.Dialogs;
using MUI.Pages;
using MUI.Resources;

namespace MUI.UGUI
{
    /// <summary>标准确认框的可选 uGUI 模板绑定；项目可替换布局或另建确认路由。</summary>
    [BindingFactory(typeof(ConfirmationViewModel))]
    public static class ConfirmationDialogBindings
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
            new BindingEntry(nameof(ConfirmationViewModel.Confirm), "Confirm", typeof(NativeControl), nameof(Button.onClick), BindingMode.OneWay, BindingEntryKind.Command, "interactable") { ComponentType = typeof(Button) },
            new BindingEntry(nameof(ConfirmationViewModel.Cancel), "Cancel", typeof(NativeControl), nameof(Button.onClick), BindingMode.OneWay, BindingEntryKind.Command, "interactable") { ComponentType = typeof(Button) }
        });

        /// <summary>创建本次界面的绑定上下文；生命周期由界面实例管理。</summary>
        public static BindingContext<ConfirmationViewModel> Create(IView view, ConfirmationViewModel model) => new Context(view, model);

        /// <summary>显式注册绑定工厂；直接使用 CreateDefinition 时已提供绑定工厂。</summary>
        public static void Register() => BindingRegistry.Register<ConfirmationViewModel>(Create, Manifest);

        private static BindingEntry TextEntry(string property, string name) => new BindingEntry(property, name, typeof(NativeControl), nameof(Text.text), BindingMode.OneWay) { ComponentType = typeof(Text) };

        private sealed class Context : BindingContext<ConfirmationViewModel>
        {
            internal Context(IView view, ConfirmationViewModel model) : base(view, model)
            {
            }

            protected override void BuildBindings(BindingBuilder<ConfirmationViewModel> builder)
            {
                builder.Property<NativeControl, string>("Title", nameof(ConfirmationViewModel.Title), model => model.Title, null, nameof(Text.text), null, (element, value) => ((Text)element.Target).text = value, componentType: typeof(Text));
                builder.Property<NativeControl, string>("Message", nameof(ConfirmationViewModel.Message), model => model.Message, null, nameof(Text.text), null, (element, value) => ((Text)element.Target).text = value, componentType: typeof(Text));
                builder.Property<NativeControl, string>("ConfirmLabel", nameof(ConfirmationViewModel.ConfirmLabel), model => model.ConfirmLabel, null, nameof(Text.text), null, (element, value) => ((Text)element.Target).text = value, componentType: typeof(Text));
                builder.Property<NativeControl, string>("CancelLabel", nameof(ConfirmationViewModel.CancelLabel), model => model.CancelLabel, null, nameof(Text.text), null, (element, value) => ((Text)element.Target).text = value, componentType: typeof(Text));
                builder.Command<NativeControl>("Confirm", model => model.Confirm, (element, handler) => element.AddCommandListener(((Button)element.Target).onClick, handler), (element, handler) => element.RemoveCommandListener(((Button)element.Target).onClick, handler), (element, enabled) => ((Button)element.Target).interactable = enabled, eventName: "onClick", interactableProperty: "interactable", componentType: typeof(Button));
                builder.Command<NativeControl>("Cancel", model => model.Cancel, (element, handler) => element.AddCommandListener(((Button)element.Target).onClick, handler), (element, handler) => element.RemoveCommandListener(((Button)element.Target).onClick, handler), (element, enabled) => ((Button)element.Target).interactable = enabled, eventName: "onClick", interactableProperty: "interactable", componentType: typeof(Button));
            }
        }
    }
}
