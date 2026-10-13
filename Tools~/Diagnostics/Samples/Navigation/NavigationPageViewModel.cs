using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>导航示例共享契约：标题与确认按钮，供本地和延迟提供方共用。</summary>
    [UIPage(typeof(string), typeof(int), Key = "demo.navigation-page", ResourceKey = "NavigationPage")]
    public partial class NavigationPageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(UnityEngine.UI.Text.text))]
        private string title;

        [Command]
        [BindCommand("Confirm", nameof(UnityEngine.UI.Button.onClick))]
        private void Confirm(CommandContext context) => context.RequestClose(42);
    }
}
