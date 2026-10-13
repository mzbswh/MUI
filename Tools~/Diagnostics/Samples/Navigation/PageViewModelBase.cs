using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>页面共用的标题与关闭行为；具体页面的生成绑定会合并这些声明。</summary>
    public abstract partial class PageViewModelBase : ViewModel
    {
        [ObservableProperty]
        [Bind("Title", nameof(TextElement.Content))]
        private string title;

        [Command]
        [BindCommand("Close", nameof(ButtonElement.Clicked))]
        private void Close(CommandContext context) => context.RequestClose();
    }
}
