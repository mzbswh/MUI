using MUI.UGUI;

namespace MUI.Samples.Tabs
{
    [UIWidget("TabPage")]
    public partial class TabPageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(UnityEngine.UI.Text.text))]
        private string title;
    }
}
