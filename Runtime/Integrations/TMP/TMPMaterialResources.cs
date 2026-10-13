using MUI.Resources;
using MUI.Unity;
using MUI.UGUI;
using TMPro;
using UnityEngine;

namespace MUI.TMP
{
    [ControlPropertyExtension]
    public sealed class TMPMaterialResources : ResourcePropertyExtension<TextMeshProUGUI, Material>
    {
        public TMPMaterialResources(NativeControl owner) : base(owner, nameof(MaterialSource), "fontSharedMaterial") { }
        public string MaterialSource { get => Source; set => Source = value; }
        [NotifiesChange]
        public ResourceSourceSnapshot MaterialSourceSnapshot => Snapshot;
        protected override void AssignAsset(TextMeshProUGUI component, Material material)
        {
            var font = component.font;
            component.fontSharedMaterial = material != null ? material : font == null ? null : font.material;
        }
        // A failed TMP setter may retain a mesh material. Do not claim successful detachment.
        protected override bool ConfirmCleared(TextMeshProUGUI component) => false;
        protected override void SuspendDisplay(TextMeshProUGUI component)
        {
            if (component == null) return;
            try { component.enabled = false; }
            finally { if (component != null) component.canvasRenderer.Clear(); }
        }
    }
}
