using System;
using System.Collections.Generic;
using MUI.Pages;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class MUISettings : ScriptableObject
    {
        [SerializeField] private List<MUILayer> layers = CreateLayers();
        [SerializeField] private PagePolicySettings defaultRules = new PagePolicySettings();
        [SerializeField] private List<PagePolicyPreset> presets = CreatePresets();
        [SerializeField] private string defaultPreset = "Default";
        [SerializeField] private string renderSortingLayer = "Default";
        [SerializeField] private int renderOrderMinimum = -16000;
        [SerializeField] private int renderOrderMaximum = 16000;
        [SerializeField] private int defaultPageOrderSpan = 32;
        [SerializeField] private int preferredOrderGap = 32;
        [SerializeField] private int preferredLayerGap = 128;
        [SerializeField] private RectTransform defaultModalBackdropPrefab = null;
        [SerializeField] private Color defaultModalBackdropColor = new Color(0, 0, 0, 0.45f);
        [SerializeField] private bool automaticFramePump = true;
        [SerializeField, Min(0)] private int cacheCapacity = 16;
        [SerializeField, Min(1)] private int queueCapacity = 64;
        [SerializeField, Min(1)] private int preloadCapacity = 32;
        [SerializeField, Min(1)] private int cleanupCapacity = 16;
        [SerializeField, Min(1)] private int terminalCapacity = 256;

        public IReadOnlyList<MUILayer> Layers => layers == null ? (IReadOnlyList<MUILayer>)Array.Empty<MUILayer>() : layers.AsReadOnly();

        /// <summary>无项目配置时使用的内置预设名称，与内置规则共用同一来源。</summary>
        public static IReadOnlyList<string> BuiltInPresetNames => CreatePresets().ConvertAll(item => item.Name).AsReadOnly();

        public IReadOnlyList<PagePolicyPreset> Presets => presets == null ? (IReadOnlyList<PagePolicyPreset>)Array.Empty<PagePolicyPreset>() : presets.AsReadOnly();

        public string DefaultPreset => defaultPreset;

        public RectTransform DefaultModalBackdropPrefab => defaultModalBackdropPrefab;

        public Color DefaultModalBackdropColor => defaultModalBackdropColor;

        public bool AutomaticFramePump => automaticFramePump;


        public int CacheCapacity => cacheCapacity;

        public int QueueCapacity => queueCapacity;

        public int PreloadCapacity => preloadCapacity;

        public int CleanupCapacity => cleanupCapacity;

        public int TerminalCapacity => terminalCapacity;

        public RenderOrderOptions CreateRenderOrderOptions()
        {
            var gaps = new Dictionary<int, int>();
            if (layers != null)
            {
                foreach (var entry in layers)
                {
                    if (entry == null)
                    {
                        continue;
                    }
                    if (entry.LayerGapAfter < -1)
                    {
                        throw new InvalidOperationException("Layer 预留间隔必须为 -1（继承）或非负数：" + entry.Name);
                    }
                    if (entry.LayerGapAfter >= 0)
                    {
                        if (gaps.ContainsKey(entry.Order))
                        {
                            throw new InvalidOperationException("层级排序值不能重复：" + entry.Order);
                        }
                        gaps.Add(entry.Order, entry.LayerGapAfter);
                    }
                }
            }
            foreach (var layer in SortingLayer.layers)
            {
                if (layer.name == renderSortingLayer)
                {
                    return new RenderOrderOptions(renderOrderMinimum, renderOrderMaximum, defaultPageOrderSpan, preferredOrderGap, layer.id, gaps, preferredLayerGap);
                }
            }
            throw new InvalidOperationException("不存在渲染 Sorting Layer：" + renderSortingLayer);
        }

        /// <summary>复制项目默认、预设及页面覆盖，返回不再引用配置资产的运行策略。</summary>
        public PagePolicy ResolvePolicy(string preset = null, string layer = null, PagePolicyOverrides overrides = null)
        {
            RequireValid();
            var policy = Resolve(layers, defaultRules, presets, preset ?? defaultPreset, layer, overrides, name);
            RequireRenderSpan(policy);
            return AttachPagePolicies(policy, preset ?? defaultPreset, layer, overrides);
        }

        private void RequireRenderSpan(PagePolicy policy)
        {
            var sorting = CreateRenderOrderOptions();
            if (policy.RenderOrderSpan > sorting.Maximum - sorting.Minimum + 1)
            {
                throw new InvalidOperationException("页面排序跨度超过宿主总范围。");
            }
        }

        public void RequireValid()
        {
            var errors = Validate();
            if (errors.Count != 0)
            {
                throw new InvalidOperationException("MUI 配置无效：\n" + string.Join("\n", errors));
            }
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            try
            {
                ModalBackdropStyle.ValidateVisualPrefab(defaultModalBackdropPrefab);
            }
            catch (InvalidOperationException error) { errors.Add(error.Message); }
            try
            {
                CreateRenderOrderOptions();
            }
            catch (Exception error)
            {
                errors.Add(error.Message);
            }
            var layerNames = new HashSet<string>(StringComparer.Ordinal);
            var layerOrders = new HashSet<int>();
            if (layers == null || layers.Count == 0)
            {
                errors.Add("至少需要一个命名层级。");
            }
            else
            {
                foreach (var entry in layers)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Name) || !layerNames.Add(entry.Name))
                    {
                        errors.Add("层级名称不能为空或重复。");
                    }
                    else if (!layerOrders.Add(entry.Order))
                    {
                        errors.Add("层级排序值不能重复：" + entry.Order);
                    }
                }
            }

            var presetNames = new HashSet<string>(StringComparer.Ordinal);
            if (presets == null || presets.Count == 0)
            {
                errors.Add("至少需要一个策略预设。");
            }
            else
            {
                foreach (var entry in presets)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Name) || !presetNames.Add(entry.Name))
                    {
                        errors.Add("预设名称不能为空或重复。");
                    }
                    else if ((entry.Overrides & ~(PagePolicyFields.All | (PagePolicyFields)2)) != 0)
                    {
                        errors.Add(entry.Name + " 包含未知的覆盖选项。");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(defaultPreset) || !presetNames.Contains(defaultPreset))
            {
                errors.Add("默认策略必须指向已声明的预设。");
            }
            if (cacheCapacity < 0 || queueCapacity < 1 || preloadCapacity < 1 || cleanupCapacity < 1 || terminalCapacity < 1)
            {
                errors.Add("缓存容量必须非负；队列、预加载、清理及终态记录容量必须大于零。");
            }
            if (errors.Count == 0)
            {
                try
                {
                    RequireRenderSpan(ResolveDefaults(layers, defaultRules));
                }
                catch (Exception error)
                {
                    errors.Add("默认规则：" + error.Message);
                }
                foreach (var entry in presets)
                {
                    try
                    {
                        RequireRenderSpan(Resolve(layers, defaultRules, presets, entry.Name, null, null, name));
                    }
                    catch (Exception error)
                    {
                        errors.Add(entry.Name + "：" + error.Message);
                    }
                }
            }
            ValidatePagePolicies(errors);
            return errors.AsReadOnly();
        }

        /// <summary>未绑定资产时沿用内置预设；不创建或自动查找 ScriptableObject。</summary>
        public static PagePolicy ResolveBuiltInPolicy(string preset = null, string layer = null, PagePolicyOverrides overrides = null)
            => Resolve(CreateLayers(), new PagePolicySettings(), CreatePresets(), preset ?? "Default", layer, overrides, "内置默认");

        private static PagePolicy Resolve(IReadOnlyList<MUILayer> layers, PagePolicySettings defaults,
            IReadOnlyList<PagePolicyPreset> presets, string preset, string layer, PagePolicyOverrides overrides, string configuration)
        {
            if (layer != null && overrides != null && overrides.Layer.HasValue)
            {
                throw new ArgumentException("页面不能同时覆盖命名层级和数值层级。");
            }
            PagePolicyPreset selected = null;
            foreach (var entry in presets)
            {
                if (entry.Name == preset)
                {
                    selected = entry;
                    break;
                }
            }
            if (selected == null || selected.Values == null)
            {
                throw new ArgumentException("未声明策略预设或预设缺少规则：" + preset, nameof(preset));
            }

            var policy = ResolveDefaults(layers, defaults);
            var presetLayer = (selected.Overrides & PagePolicyFields.Layer) != 0
                ? FindLayer(layers, selected.Values.Layer).Order : policy.Layer;
            policy = selected.Values.CreateOverrides(selected.Overrides, presetLayer).Apply(policy);
            var resolvedLayer = layer == null ? policy.Layer : FindLayer(layers, layer).Order;
            if (layer != null)
            {
                policy = new PagePolicyOverrides { Layer = resolvedLayer }.Apply(policy);
            }
            if (overrides != null)
            {
                policy = overrides.Apply(policy);
            }
            string layerName = null;
            foreach (var entry in layers)
            {
                if (entry.Order == policy.Layer)
                {
                    layerName = entry.Name;
                    break;
                }
            }
            return new PagePolicyOverrides().Apply(policy, layerName, preset, configuration);
        }

        private static PagePolicy ResolveDefaults(IReadOnlyList<MUILayer> layers, PagePolicySettings defaults)
        {
            if (defaults == null)
            {
                throw new InvalidOperationException("默认规则不能为空。");
            }
            return defaults.CreateOverrides(PagePolicyFields.All, FindLayer(layers, defaults.Layer).Order).Apply(PagePolicy.Default);
        }

        private static MUILayer FindLayer(IReadOnlyList<MUILayer> layers, string name)
        {
            foreach (var entry in layers)
            {
                if (entry.Name == name)
                {
                    return entry;
                }
            }
            throw new ArgumentException("未声明层级：" + name);
        }

        private static List<MUILayer> CreateLayers() => new List<MUILayer>
        {
            new MUILayer("Background", -100), new MUILayer("Default", 0),
            new MUILayer("Popup", 100), new MUILayer("Notice", 200)
        };

        private static List<PagePolicyPreset> CreatePresets() => new List<PagePolicyPreset>
        {
            new PagePolicyPreset("Default", PagePolicyFields.None),
            new PagePolicyPreset("FullScreen", PagePolicyFields.Coverage | PagePolicyFields.PageScope)
            {
                Values = new PagePolicySettings { PageRole = PageRole.Main, Coverage = CoveragePolicy.Hide }
            },
            new PagePolicyPreset("Popup", PagePolicyFields.PageScope | PagePolicyFields.Layer | PagePolicyFields.Coverage | PagePolicyFields.Modal)
            {
                Values = new PagePolicySettings { Layer = "Popup", PageRole = PageRole.Overlay, Coverage = CoveragePolicy.BlockInput, Modal = true }
            },
            new PagePolicyPreset("Notice", PagePolicyFields.PageScope | PagePolicyFields.Layer | PagePolicyFields.Coverage |
                PagePolicyFields.Focus | PagePolicyFields.Modal | PagePolicyFields.Back | PagePolicyFields.Input)
            {
                Values = new PagePolicySettings { Layer = "Notice", PageRole = PageRole.Independent, ReceivesInput = false, TakesFocus = false, BackBehavior = BackBehavior.Ignore }
            },
            new PagePolicyPreset("Background", PagePolicyFields.PageScope | PagePolicyFields.Layer | PagePolicyFields.Coverage |
                PagePolicyFields.Focus | PagePolicyFields.Modal | PagePolicyFields.Back | PagePolicyFields.Input)
            {
                Values = new PagePolicySettings { Layer = "Background", PageRole = PageRole.Independent, ReceivesInput = false, TakesFocus = false, BackBehavior = BackBehavior.Ignore }
            }
        };
    }
}
