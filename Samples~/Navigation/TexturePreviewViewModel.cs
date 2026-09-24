using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>
    /// 纹理预览的生成绑定示例；Preview 节点挂 RawImageElement。
    /// 模型仅借用纹理，资源提供方负责在解绑并清空显示后归还纹理。
    /// </summary>
    [ViewContract("TexturePreview")]
    public partial class TexturePreviewViewModel : ViewModel
    {
        [ObservableProperty]
        [Bind("Preview", nameof(RawImageElement.Texture), ElementType = typeof(RawImageElement))]
        private Texture texture;
        [ObservableProperty]
        [Bind("Preview", nameof(RawImageElement.Color), ElementType = typeof(RawImageElement))]
        private Color tint = Color.white;
        [ObservableProperty]
        [Bind("Preview", nameof(RawImageElement.UVRect), ElementType = typeof(RawImageElement))]
        private Rect samplingRect = new Rect(0, 0, 1, 1);
    }
}
