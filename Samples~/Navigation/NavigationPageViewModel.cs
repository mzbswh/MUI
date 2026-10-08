using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>导航示例共享契约：标题与确认按钮，供本地和延迟提供方共用。</summary>
    [ViewContract("NavigationPage")]
    [MUI.Navigation.ViewRoute(typeof(string), typeof(int), Key = "demo.navigation-page")]
    public partial class NavigationPageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(TextElement.Content))]
        private string title;

        [Command]
        [BindCommand("Confirm", nameof(ButtonElement.Clicked))]
        private void Confirm(CommandContext context) => context.Complete(42);
    }
}
