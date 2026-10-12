using System;
namespace MUI.Pages
{
    public sealed partial class PageManager
    {
        private readonly PageRenderOrderAllocator renderOrdering;
        private RenderOrderOptions renderOrder => renderOrdering.Options;
        public RenderOrderOptions RenderOrder => renderOrdering.Options;
        private void ReserveRenderOrder(PageInstance instance) => renderOrdering.Reserve(instance);
        private void ReleaseRenderOrder(PageInstance instance) => renderOrdering.Release(instance);
        private void RefreshRenderOrders() => renderOrdering.Refresh(ownership);
        private void ApplyRenderOrder(PageInstance instance)
        {
            var slot = renderOrdering.GetSlot(instance.Handle);
            if (slot == null || instance.View == null ||
                !slot.Start.HasValue || slot.AppliedStart == slot.Start)
            {
                return;
            }
            if (!(instance.View is IRenderOrderedView view))
            {
                throw new InvalidOperationException("启用排序区间的宿主要求 View 实现 IRenderOrderedView。");
            }
            using (EnterCallback(instance))
            {
                view.SetRenderOrder(renderOrder.SortingLayerId, slot.Start.Value, slot.Span, renderOrder.Maximum + 1);
            }
            slot.AppliedStart = slot.Start;
        }
    }
}
