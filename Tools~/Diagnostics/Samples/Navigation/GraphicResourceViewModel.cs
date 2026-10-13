using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>
    /// 共用图形属性的生成绑定示例；Icon、Preview、Label 分别适配图片、纹理和旧版文字。
    /// 三个控件在绑定前分别配置适用的材质加载器与生命周期。
    /// </summary>
    [UIWidget("GraphicResource")]
    public partial class GraphicResourceViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Icon", nameof(GraphicResources.MaterialSource))]
        [Bind("Preview", nameof(GraphicResources.MaterialSource))]
        private string materialKey;
        [ObservableProperty]
        [Bind("Label", nameof(GraphicResources.MaterialSource))]
        private string textMaterialKey;
        [ObservableProperty]
        [Bind("Label", nameof(UnityEngine.UI.Text.font))]
        private Font labelFont;
        [ObservableProperty]
        [Bind("Label", nameof(UnityEngine.UI.Text.fontSize))]
        private int labelFontSize = 24;
        [ObservableProperty]
        [Bind("Label", nameof(UnityEngine.UI.Text.alignment))]
        private TextAnchor labelAlignment = TextAnchor.MiddleCenter;
        [ObservableProperty]
        [Bind("Label", nameof(UnityEngine.UI.Text.supportRichText))]
        private bool labelRichText;
        [ObservableProperty]
        [Bind("Label", nameof(UnityEngine.UI.Text.resizeTextMinSize))]
        private int labelMinFontSize = 12;
        [ObservableProperty]
        [Bind("Label", nameof(UnityEngine.UI.Text.resizeTextMaxSize))]
        private int labelMaxFontSize = 32;
        [ObservableProperty]
        [Bind("Label", nameof(UnityEngine.UI.Text.resizeTextForBestFit))]
        private bool labelBestFit;
        [ObservableProperty]
        [Bind("Icon", nameof(UnityEngine.UI.Image.type))]
        private UnityEngine.UI.Image.Type iconImageType = UnityEngine.UI.Image.Type.Simple;
        [ObservableProperty]
        [Bind("Icon", nameof(UnityEngine.UI.Image.preserveAspect))]
        private bool iconPreserveAspect = true;
        [ObservableProperty]
        [Bind("Icon", nameof(UnityEngine.UI.Image.color))]
        [Bind("Preview", nameof(UnityEngine.UI.RawImage.color))]
        [Bind("Label", nameof(UnityEngine.UI.Text.color))]
        private Color tint = Color.white;
        [ObservableProperty]
        [Bind("Icon", nameof(UnityEngine.UI.Image.raycastTarget))]
        [Bind("Preview", nameof(UnityEngine.UI.RawImage.raycastTarget))]
        [Bind("Label", nameof(UnityEngine.UI.Text.raycastTarget))]
        private bool receiveRaycasts;
        [ObservableProperty]
        [Bind("Icon", nameof(UnityEngine.UI.Image.maskable))]
        [Bind("Preview", nameof(UnityEngine.UI.RawImage.maskable))]
        [Bind("Label", nameof(UnityEngine.UI.Text.maskable))]
        private bool useMask = true;
    }
}
