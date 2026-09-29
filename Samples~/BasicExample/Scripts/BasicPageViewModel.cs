using MUI.UGUI;

namespace MUI.BasicExample
{
    [ViewContract("BasicView")]
    [Navigation.ViewRoute(typeof(string), typeof(int), Key = "basic.page",
        SupportsSynchronousLifecycle = true)]
    public partial class BasicPageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(TextElement.Content))]
        private string title;

        [Command]
        [BindCommand("Confirm", nameof(ButtonElement.Clicked))]
        private void Confirm(CommandContext context) => context.Complete(42);
    }
}
