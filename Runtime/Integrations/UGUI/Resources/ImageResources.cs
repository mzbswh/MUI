using MUI.Resources;
using MUI.Unity;
using UnityEngine;

namespace MUI.UGUI
{
    [ControlPropertyExtension]
    public sealed class ImageResources : ResourcePropertyExtension<UnityEngine.UI.Image, Sprite>
    {
        public ImageResources(NativeControl owner) : base(owner, nameof(SpriteSource), "sprite") { }
        public string SpriteSource { get => Source; set => Source = value; }
        [NotifiesChange]
        public ResourceSourceSnapshot SpriteSourceSnapshot => Snapshot;
        protected override void AssignAsset(UnityEngine.UI.Image component, Sprite asset) => component.sprite = asset;
        protected override bool ConfirmCleared(UnityEngine.UI.Image component)
        {
            if (component == null || !(ReferenceEquals(component.sprite, null) && ReferenceEquals(component.overrideSprite, null))) return false;
            component.canvasRenderer.Clear();
            return true;
        }
        protected override void SuspendDisplay(UnityEngine.UI.Image component)
        {
            if (component == null) return;
            try { component.enabled = false; }
            finally { if (component != null) component.canvasRenderer.Clear(); }
        }
    }
}
