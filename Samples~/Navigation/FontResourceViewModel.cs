using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>独立字体资源绑定示例，加载器由项目在 View 激活前配置。</summary>
    [ViewContract("FontResourceView")]
    public partial class FontResourceViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.Content))]
        private string content = "Font resource binding";
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.FontSource))]
        private string fontKey;
    }
}
