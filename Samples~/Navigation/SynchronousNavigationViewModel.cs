using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>纯同步示例契约：一个标题和一个确认按钮，无异步命令。</summary>
    [ViewContract("SynchronousNavigationView")]
    [MUI.Navigation.ViewRoute(typeof(string), typeof(int), Key = "demo.pure-sync", SupportsSynchronousLifecycle = true)]
    public partial class SynchronousNavigationViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(TextElement.Content))]
        private string title;

        [Command]
        [BindCommand("Confirm", nameof(ButtonElement.Clicked))]
        private void Confirm(CommandContext context) => context.Complete(42);
    }
}
