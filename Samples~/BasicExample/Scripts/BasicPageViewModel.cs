using MUI.UGUI;

namespace MUI.BasicExample
{
    [ViewContract("BasicView")]
    [Navigation.ViewRoute(typeof(Unit), typeof(int), Key = "basic.page")]
    public partial class BasicPageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(ITextElement.Content))]
        private string title = "MUI basic example";

        [Command]
        [BindCommand("Confirm", nameof(ButtonElement.Clicked))]
        private void Confirm(CommandContext context) => context.Complete(42);
    }
}
