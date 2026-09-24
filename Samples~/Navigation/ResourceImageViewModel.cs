using MUI.UGUI;

namespace MUI.Samples.Navigation
{
    /// <summary>
    /// 资源键绑定示例：View 在绑定前配置图像与字体控件的加载器及生命周期。
    /// 模型只提供资源键，不接触原生对象或资源凭证。
    /// </summary>
    [ViewContract("ResourceImage")]
    public partial class ResourceImageViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Icon", nameof(ImageElement.SpriteSource), ElementType = typeof(ImageElement))]
        private string iconKey;
        [ObservableProperty]
        [Bind("Preview", nameof(RawImageElement.TextureSource), ElementType = typeof(RawImageElement))]
        private string previewKey;
        [ObservableProperty]
        [Bind("FontPreview", nameof(TextElement.FontSource))]
        [NotifyPropertyChangedFor(nameof(FontCaption))]
        private string fontKey;

        /// <summary>说明文字从资源键计算，生成的依赖通知负责刷新绑定，不存储重复状态。</summary>
        [Bind("FontPreview", nameof(TextElement.Content))]
        public string FontCaption => string.IsNullOrEmpty(FontKey) ? "Font cleared" : "Requested font: " + FontKey;

        /// <summary>显式通知同一字体键，演示失败后的重新加载。</summary>
        public void RefreshFont() => OnPropertyChanged(nameof(FontKey));

        /// <summary>显式重试当前图标键；同值 setter 不会产生新的属性通知。</summary>
        public void RefreshIcon() => OnPropertyChanged(nameof(IconKey));

        /// <summary>显式重试当前纹理键，不改变当前模型数据。</summary>
        public void RefreshPreview() => OnPropertyChanged(nameof(PreviewKey));
    }
}
