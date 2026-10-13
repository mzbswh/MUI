using MUI.Resources;
using MUI.Unity;
using UnityEngine;

namespace MUI.UGUI
{
    [ControlPropertyExtension]
    public sealed class TextResources : ResourcePropertyExtension<UnityEngine.UI.Text, Font>
    {
        public TextResources(NativeControl owner) : base(owner, nameof(FontSource), "font") { }
        public string FontSource { get => Source; set => Source = value; }
        [NotifiesChange]
        public ResourceSourceSnapshot FontSourceSnapshot => Snapshot;
        protected override void AssignAsset(UnityEngine.UI.Text component, Font asset) => component.font = asset;
        protected override bool ConfirmCleared(UnityEngine.UI.Text component)
        {
            if (component == null || !(ReferenceEquals(component.font, null))) return false;
            component.canvasRenderer.Clear();
            return true;
        }
        protected override void SuspendDisplay(UnityEngine.UI.Text component)
        {
            if (component == null) return;
            try { component.enabled = false; }
            finally { if (component != null) component.canvasRenderer.Clear(); }
        }
    }
}
