using MUI.Resources;
using MUI.Unity;
using UnityEngine;

namespace MUI.UGUI
{
    [ControlPropertyExtension]
    public sealed class GraphicResources : ResourcePropertyExtension<UnityEngine.UI.Graphic, Material>
    {
        public GraphicResources(NativeControl owner) : base(owner, nameof(MaterialSource), "material") { }
        public string MaterialSource { get => Source; set => Source = value; }
        [NotifiesChange]
        public ResourceSourceSnapshot MaterialSourceSnapshot => Snapshot;
        protected override void AssignAsset(UnityEngine.UI.Graphic component, Material asset) => component.material = asset;
        protected override bool ConfirmCleared(UnityEngine.UI.Graphic component)
        {
            if (component == null) return false;
            var type = component.GetType();
            if (type != typeof(UnityEngine.UI.Image) && type != typeof(UnityEngine.UI.RawImage) && type != typeof(UnityEngine.UI.Text)) return false;
            component.material = null; component.canvasRenderer.Clear();
            return true;
        }
        protected override void SuspendDisplay(UnityEngine.UI.Graphic component)
        {
            if (component == null) return;
            try { component.enabled = false; }
            finally { if (component != null) component.canvasRenderer.Clear(); }
        }
    }
}
