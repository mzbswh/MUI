using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>
    /// 共用图形属性的生成绑定示例；Icon、Preview、Label 分别适配图片、纹理和旧版文字。
    /// 三个控件在绑定前分别配置适用的材质加载器与生命周期。
    /// </summary>
    [ViewContract("GraphicResource")]
    public partial class GraphicResourceViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Icon", nameof(ImageElement.MaterialSource))]
        [Bind("Preview", nameof(RawImageElement.MaterialSource))]
        private string materialKey;
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.MaterialSource))]
        private string textMaterialKey;
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.Font))]
        private Font labelFont;
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.FontSize))]
        private int labelFontSize = 24;
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.Alignment))]
        private TextAnchor labelAlignment = TextAnchor.MiddleCenter;
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.RichText))]
        private bool labelRichText;
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.MinFontSize))]
        private int labelMinFontSize = 12;
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.MaxFontSize))]
        private int labelMaxFontSize = 32;
        [ObservableProperty]
        [Bind("Label", nameof(TextElement.BestFit))]
        private bool labelBestFit;
        [ObservableProperty]
        [Bind("Icon", nameof(ImageElement.ImageType))]
        private UnityEngine.UI.Image.Type iconImageType = UnityEngine.UI.Image.Type.Simple;
        [ObservableProperty]
        [Bind("Icon", nameof(ImageElement.PreserveAspect))]
        private bool iconPreserveAspect = true;
        [ObservableProperty]
        [Bind("Icon", nameof(ImageElement.Color))]
        [Bind("Preview", nameof(RawImageElement.Color))]
        [Bind("Label", nameof(TextElement.Color))]
        private Color tint = Color.white;
        [ObservableProperty]
        [Bind("Icon", nameof(ImageElement.RaycastTarget))]
        [Bind("Preview", nameof(RawImageElement.RaycastTarget))]
        [Bind("Label", nameof(TextElement.RaycastTarget))]
        private bool receiveRaycasts;
        [ObservableProperty]
        [Bind("Icon", nameof(ImageElement.Maskable))]
        [Bind("Preview", nameof(RawImageElement.Maskable))]
        [Bind("Label", nameof(TextElement.Maskable))]
        private bool useMask = true;
    }
}
