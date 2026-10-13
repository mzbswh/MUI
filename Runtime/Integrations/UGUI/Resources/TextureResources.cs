using MUI.Resources;
using MUI.Unity;
using UnityEngine;

namespace MUI.UGUI
{
    [ControlPropertyExtension]
    public sealed class TextureResources : ResourcePropertyExtension<UnityEngine.UI.RawImage, Texture>
    {
        public TextureResources(NativeControl owner) : base(owner, nameof(TextureSource), "texture") { }
        public string TextureSource { get => Source; set => Source = value; }
        [NotifiesChange]
        public ResourceSourceSnapshot TextureSourceSnapshot => Snapshot;
        protected override void AssignAsset(UnityEngine.UI.RawImage component, Texture asset) => component.texture = asset;
        protected override bool ConfirmCleared(UnityEngine.UI.RawImage component)
        {
            if (component == null || !(ReferenceEquals(component.texture, null))) return false;
            component.canvasRenderer.Clear();
            return true;
        }
        protected override void SuspendDisplay(UnityEngine.UI.RawImage component)
        {
            if (component == null) return;
            try { component.enabled = false; }
            finally { if (component != null) component.canvasRenderer.Clear(); }
        }
    }
}
