using System;
using System.Collections.Generic;
using UnityEngine;

namespace MUI.UGUI
{
    [Serializable]
    public sealed class ViewRenderOrderTarget
    {
        public Component Target;
        [Min(2)] public int Offset = 2;
    }

    public sealed partial class View : IRenderOrderedView
    {
        [SerializeField] private List<ViewRenderOrderTarget> renderOrderTargets = new List<ViewRenderOrderTarget>();
        private readonly Dictionary<Component, int> dynamicRenderTargets = new Dictionary<Component, int>();
        private readonly Dictionary<Renderer, bool> rendererVisibility = new Dictionary<Renderer, bool>();
        private Canvas orderedCanvas;
        private bool hasRenderOrder;
        private int renderSortingLayerId;
        private int renderOrderStart;
        private int renderOrderSpan;
        private int closingBarrierOrder;

        public bool HasRenderOrder => hasRenderOrder;

        public int RenderOrderStart => renderOrderStart;

        public int RenderOrderSpan => renderOrderSpan;

        public IReadOnlyList<ViewRenderOrderTarget> RenderOrderTargets => renderOrderTargets;

        /// <summary>动态生成的特效/Canvas 使用相对偏移；目标必须属于本页面，偏移 0、1 由框架保留。</summary>
        public void RegisterRenderOrderTarget(Component target, int offset)
        {
            RegisterRenderOrderTargets(new[] { new ViewRenderOrderTarget { Target = target, Offset = offset } });
        }

        /// <summary>一次登记整组动态特效，避免单项登记时其余 Renderer 尚未纳入管理。</summary>
        public void RegisterRenderOrderTargets(IReadOnlyList<ViewRenderOrderTarget> targets)
        {
            RequireAlive();
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }
            var previous = new Dictionary<Component, int>(dynamicRenderTargets);
            try
            {
                foreach (var binding in targets)
                {
                    if (binding == null)
                    {
                        throw new ArgumentException("排序目标不能为空。", nameof(targets));
                    }
                    ValidateRenderTarget(binding.Target, binding.Offset, hasRenderOrder ? renderOrderSpan : int.MaxValue);
                    dynamicRenderTargets[binding.Target] = binding.Offset;
                }
                RefreshRenderOrderTargets();
            }
            catch
            {
                dynamicRenderTargets.Clear();
                foreach (var binding in previous)
                {
                    dynamicRenderTargets.Add(binding.Key, binding.Value);
                }
                throw;
            }
        }

        /// <summary>节点销毁或移出页面后可移除登记；仍在页面内的可渲染对象必须继续受排序管理。</summary>
        public void UnregisterRenderOrderTarget(Component target)
        {
            RequireAlive();
            if (ReferenceEquals(target, null))
            {
                return;
            }
            if (target != null && target.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException("请先销毁目标或将它移出页面，再移除排序登记。");
            }
            dynamicRenderTargets.Remove(target);
            if (target is Renderer renderer && renderer != null && rendererVisibility.TryGetValue(renderer, out var original))
            {
                renderer.forceRenderingOff = original;
                rendererVisibility.Remove(renderer);
            }
        }

        public void SetRenderOrder(int sortingLayerId, int start, int span, int barrierOrder)
        {
            RequireAlive();
            if (span < 2 || start < short.MinValue || (long)start + span - 1 >= barrierOrder || barrierOrder > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            if (!SortingLayer.IsValid(sortingLayerId))
            {
                throw new ArgumentException("页面 Sorting Layer 不存在。", nameof(sortingLayerId));
            }
            foreach (var ancestor in GetComponentsInParent<View>(true))
            {
                if (ancestor != this && ancestor.hasRenderOrder)
                {
                    throw new InvalidOperationException("独立页面排序区间不能嵌套在另一个受管理页面内。");
                }
            }
            foreach (var child in GetComponentsInChildren<View>(true))
            {
                if (child != this && child.hasRenderOrder)
                {
                    throw new InvalidOperationException("页面内部不能包含另一个独立分配排序区间的页面。");
                }
            }
            // 即使后续排序契约校验失败，隐藏候选和失败清理也不能留下可见的原生特效。
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!rendererVisibility.ContainsKey(renderer))
                {
                    rendererVisibility.Add(renderer, renderer.forceRenderingOff);
                }
            }
            ApplyRendererVisibility();
            var targets = CollectRenderTargets(span);
            var root = GetComponentInParent<Canvas>(true);
            if (root == null)
            {
                throw new InvalidOperationException("页面排序需要挂载在 Canvas 下。");
            }
            foreach (var target in targets.Keys)
            {
                if (target is Renderer && root.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    throw new InvalidOperationException("普通 Renderer 特效不能与 Overlay UI 穿插排序，请使用相机 Canvas 或 UI 粒子渲染方案。");
                }
            }
            orderedCanvas = GetComponent<Canvas>();
            if (orderedCanvas == null)
            {
                var channels = root.additionalShaderChannels;
                orderedCanvas = gameObject.AddComponent<Canvas>();
                orderedCanvas.additionalShaderChannels = channels;
            }
            EnsureOrderedRaycaster(gameObject);
            hasRenderOrder = true;
            renderSortingLayerId = sortingLayerId;
            renderOrderStart = start;
            renderOrderSpan = span;
            closingBarrierOrder = barrierOrder;
            SetCanvasOrder(orderedCanvas, start + 1);
            ApplyRenderTargets(targets);
            ApplyModalRenderOrder();
        }

        /// <summary>用于运行时新增内部 Canvas/Renderer；正常导航只在区间实际变化时写入排序值。</summary>
        public void RefreshRenderOrderTargets()
        {
            if (!hasRenderOrder)
            {
                return;
            }
            SetRenderOrder(renderSortingLayerId, renderOrderStart, renderOrderSpan, closingBarrierOrder);
        }

        private Dictionary<Component, int> CollectRenderTargets(int span)
        {
            foreach (var target in new List<Component>(dynamicRenderTargets.Keys))
            {
                if (target == null)
                {
                    dynamicRenderTargets.Remove(target);
                }
            }
            foreach (var renderer in new List<Renderer>(rendererVisibility.Keys))
            {
                if (renderer == null)
                {
                    rendererVisibility.Remove(renderer);
                }
            }
            var targets = new Dictionary<Component, int>();
            foreach (var binding in renderOrderTargets)
            {
                if (binding == null)
                {
                    throw new InvalidOperationException("页面排序目标不能为空。");
                }
                ValidateRenderTarget(binding.Target, binding.Offset, span);
                if (targets.ContainsKey(binding.Target))
                {
                    throw new InvalidOperationException("页面排序目标不能重复配置。");
                }
                targets.Add(binding.Target, binding.Offset);
            }
            foreach (var binding in dynamicRenderTargets)
            {
                if (binding.Key == null)
                {
                    continue;
                }
                ValidateRenderTarget(binding.Key, binding.Value, span);
                targets[binding.Key] = binding.Value;
            }
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.gameObject != gameObject && canvas.overrideSorting && !targets.ContainsKey(canvas))
                {
                    throw new InvalidOperationException("独立排序的子 Canvas 必须配置页面相对 Order：" + canvas.name);
                }
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!targets.ContainsKey(renderer))
                {
                    throw new InvalidOperationException("Renderer 必须配置页面相对 Order：" + renderer.name);
                }
            }
            return targets;
        }

        private void ValidateRenderTarget(Component target, int offset, int span)
        {
            if (target == null || (!(target is Canvas) && !(target is Renderer)) ||
                target.gameObject == gameObject || !target.transform.IsChildOf(transform) || offset < 2 || offset >= span)
            {
                throw new ArgumentException("排序目标必须是页面内的 Canvas/Renderer 子节点，相对偏移必须在 2～页面跨度减 1 内。");
            }
            // SortingGroup 会接管 Renderer 的外部排序；当前契约禁止绕过页面区间。
            foreach (var component in target.GetComponentsInParent<Behaviour>(true))
            {
                if (component != null && component.enabled && component.GetType().FullName == "UnityEngine.Rendering.SortingGroup")
                {
                    throw new InvalidOperationException("页面排序目标不能位于启用的 SortingGroup 内。");
                }
            }
        }

        private void ApplyRenderTargets(Dictionary<Component, int> targets)
        {
            foreach (var binding in targets)
            {
                var order = renderOrderStart + binding.Value;
                if (binding.Key is Canvas canvas)
                {
                    SetCanvasOrder(canvas, order);
                    EnsureOrderedRaycaster(canvas.gameObject);
                }
                else if (binding.Key is Renderer renderer)
                {
                    if (!rendererVisibility.ContainsKey(renderer))
                    {
                        rendererVisibility.Add(renderer, renderer.forceRenderingOff);
                    }
                    if (renderer.sortingLayerID != renderSortingLayerId)
                    {
                        renderer.sortingLayerID = renderSortingLayerId;
                    }
                    if (renderer.sortingOrder != order)
                    {
                        renderer.sortingOrder = order;
                    }
                }
            }
            ApplyRendererVisibility();
        }

        private void ApplyRendererVisibility()
        {
            // Prefab 配置的 Renderer 在首次隐藏准备阶段就受门控，不必等到导航提交。
            if (renderOrderTargets != null)
            {
                foreach (var binding in renderOrderTargets)
                {
                    if (binding != null && binding.Target is Renderer renderer && renderer != null &&
                        renderer.transform.IsChildOf(transform) && !rendererVisibility.ContainsKey(renderer))
                    {
                        rendererVisibility.Add(renderer, renderer.forceRenderingOff);
                    }
                }
            }
            var visible = !disposed && (retainingVisuals ? retainedVisible && exitVisible : hostVisible && localVisible);
            foreach (var binding in rendererVisibility)
            {
                if (binding.Key != null && binding.Key.transform.IsChildOf(transform))
                {
                    var hidden = binding.Value || !visible;
                    if (binding.Key.forceRenderingOff != hidden)
                    {
                        binding.Key.forceRenderingOff = hidden;
                    }
                }
            }
        }

        private void SetCanvasOrder(Canvas canvas, int order)
        {
            if (!canvas.overrideSorting)
            {
                canvas.overrideSorting = true;
            }
            if (canvas.sortingLayerID != renderSortingLayerId)
            {
                canvas.sortingLayerID = renderSortingLayerId;
            }
            if (canvas.sortingOrder != order)
            {
                canvas.sortingOrder = order;
            }
        }

        private static void EnsureOrderedRaycaster(GameObject target)
        {
            if (target.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                var parent = target.transform.parent;
                var template = parent == null ? null : parent.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>(true);
                var raycaster = target.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                if (template != null)
                {
                    raycaster.ignoreReversedGraphics = template.ignoreReversedGraphics;
                    raycaster.blockingObjects = template.blockingObjects;
                    raycaster.blockingMask = template.blockingMask;
                }
            }
        }

        private bool OwnsRenderCanvas(Canvas canvas)
        {
            if (!hasRenderOrder)
            {
                return false;
            }
            if (canvas == orderedCanvas || dynamicRenderTargets.ContainsKey(canvas))
            {
                return true;
            }
            foreach (var binding in renderOrderTargets)
            {
                if (binding != null && binding.Target == canvas)
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyModalRenderOrder()
        {
            if (!hasRenderOrder || modalBarrier == null)
            {
                return;
            }
            var canvas = modalBarrier.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = modalBarrier.AddComponent<Canvas>();
            }
            SetCanvasOrder(canvas, renderOrderStart);
            EnsureOrderedRaycaster(modalBarrier);
            modalBarrier.GetComponent<ModalPointerBarrier>().SetClosingRenderOrder(renderSortingLayerId, closingBarrierOrder);
        }
    }
}
