using System;
using System.Collections.Generic;
using System.Linq;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private readonly RenderOrderOptions renderOrder;
        private readonly Dictionary<ViewHandle, RenderSlot> renderSlots = new Dictionary<ViewHandle, RenderSlot>();
        private int reservedRenderOrders;

        public RenderOrderOptions RenderOrder => renderOrder;

        private void ReserveRenderOrder(ViewInstance instance)
        {
            if (renderOrder == null)
            {
                return;
            }
            var span = instance.Route.Policy.RenderOrderSpan == 0 ? renderOrder.DefaultPageSpan : instance.Route.Policy.RenderOrderSpan;
            if (span > renderOrder.Maximum - renderOrder.Minimum + 1 - reservedRenderOrders)
            {
                throw new NavigationPreparationRejectedException(OpenRejection.RenderOrderCapacity, "宿主排序空间不足，退出中的页面仍占用区间。");
            }
            renderSlots.Add(instance.Handle, new RenderSlot(instance, span));
            reservedRenderOrders += span;
        }

        private void ReleaseRenderOrder(ViewInstance instance)
        {
            if (renderSlots.TryGetValue(instance.Handle, out var slot))
            {
                renderSlots.Remove(instance.Handle);
                reservedRenderOrders -= slot.Span;
            }
        }

        private void RefreshRenderOrders()
        {
            if (renderOrder == null || renderSlots.Count == 0)
            {
                return;
            }
            var ordered = renderSlots.Values.Select(slot => slot.Instance)
                .OrderBy(instance => instance.Route.Policy.Layer).ThenBy(instance => instance.Order).ThenBy(instance => instance.Handle.Id).ToArray();
            ordered = ownership.OrderPresentation(ordered);
            var slots = Array.ConvertAll(ordered, instance => renderSlots[instance.Handle]);
            var cursor = renderOrder.Minimum;
            foreach (var slot in slots)
            {
                // 显式置顶只使移动页面失去锚点，保留其他页面的绝对 Order。
                if (slot.NavigationOrder != slot.Instance.Order)
                {
                    slot.Start = null;
                    slot.NavigationOrder = slot.Instance.Order;
                }
                if (slot.Start.HasValue && slot.Start.Value < cursor)
                {
                    slot.Start = null;
                }
                if (slot.Start.HasValue)
                {
                    cursor = slot.Start.Value + slot.Span;
                }
            }
            for (var index = 0; index < slots.Length;)
            {
                if (slots[index].Start.HasValue)
                {
                    index++;
                    continue;
                }
                var end = index;
                var required = 0;
                while (end < slots.Length && !slots[end].Start.HasValue)
                {
                    required += slots[end++].Span;
                }
                var lower = index == 0 ? renderOrder.Minimum : slots[index - 1].Start.Value + slots[index - 1].Span;
                var upper = end == slots.Length ? renderOrder.Maximum + 1 : slots[end].Start.Value;
                if (upper - lower < required)
                {
                    // 先扩大右侧局部窗口；到达总范围末端后才向左整理。
                    if (end < slots.Length)
                    {
                        slots[end].Start = null;
                    }
                    else if (index > 0)
                    {
                        slots[--index].Start = null;
                    }
                    else
                    {
                        throw new InvalidOperationException("排序容量账本与分配区间不一致。");
                    }
                    continue;
                }
                long desiredGaps = 0;
                for (var i = index; i <= end; i++)
                {
                    desiredGaps += GetBoundaryGap(slots, i);
                }
                var available = upper - lower - required;
                var gapBudget = Math.Min(desiredGaps, available);
                cursor = lower;
                for (var i = index; i < end; i++)
                {
                    // 预留不足时按比例缩小；不移动仍有效的区间锚点来强行补齐预留。
                    cursor += desiredGaps == 0 ? 0 : (int)(GetBoundaryGap(slots, i) * gapBudget / desiredGaps);
                    slots[i].Start = cursor;
                    cursor += slots[i].Span;
                }
                index = end;
            }
        }

        private int GetBoundaryGap(RenderSlot[] slots, int index)
        {
            if (index == 0 || index == slots.Length)
            {
                return renderOrder.PreferredGap;
            }
            var leftLayer = slots[index - 1].Instance.Route.Policy.Layer;
            var rightLayer = slots[index].Instance.Route.Policy.Layer;
            return leftLayer == rightLayer ? renderOrder.PreferredGap : renderOrder.GetLayerGapAfter(leftLayer);
        }

        private void ApplyRenderOrder(ViewInstance instance)
        {
            if (!renderSlots.TryGetValue(instance.Handle, out var slot) || instance.View == null ||
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

        private sealed class RenderSlot
        {
            internal readonly ViewInstance Instance;
            internal readonly int Span;
            internal long NavigationOrder;
            internal int? Start;
            internal int? AppliedStart;

            internal RenderSlot(ViewInstance instance, int span)
            {
                Instance = instance;
                Span = span;
                NavigationOrder = instance.Order;
            }
        }
    }
}
