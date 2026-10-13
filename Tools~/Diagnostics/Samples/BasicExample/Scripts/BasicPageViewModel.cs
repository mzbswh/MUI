using MUI.UGUI;

namespace MUI.BasicExample
{
    [UIPage(typeof(Unit), typeof(int), Key = "basic.page", ResourceKey = "BasicView")]
    public partial class BasicPageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(TextElement.Content))]
        private string title = "MUI basic example";

        [Command]
        [BindCommand("Confirm", nameof(ButtonElement.Clicked))]
        private void Confirm(CommandContext context) => context.RequestClose(42);
    }
}
