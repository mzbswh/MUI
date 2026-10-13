using MUI.UGUI;
namespace MUI.Samples.Tabs
{
    [UIWidget("QuestTab")]
    public partial class QuestTabViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(TextElement.Content))]
        private string title;
    }
}
