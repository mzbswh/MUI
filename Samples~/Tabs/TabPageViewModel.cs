using MUI.UGUI;

namespace MUI.Samples.Tabs
{
    [ViewContract("TabPage")]
    public partial class TabPageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(TextElement.Content))]
        private string title;
    }
}
