using MUI.UGUI;

namespace MUI.BasicExample
{
    [UIPage(typeof(Unit), typeof(int), Key = "basic.page", ResourceKey = "BasicView")]
    public partial class BasicPageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(UnityEngine.UI.Text.text))]
        private string title = "MUI basic example";

        [Command]
        [BindCommand("Confirm", nameof(UnityEngine.UI.Button.onClick))]
        private void Confirm(CommandContext context) => context.RequestClose(42);
    }
}
