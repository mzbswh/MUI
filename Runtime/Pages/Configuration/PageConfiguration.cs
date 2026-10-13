using System;
using System.Collections.Generic;

namespace MUI.Pages
{
    [Serializable]
    public sealed partial class PageConfiguration
    {
        public string Name = "MUI";
        public List<MUILayer> Layers = CreateLayers();
        public PagePolicySettings DefaultRules = new PagePolicySettings();
        public List<PagePolicyPreset> Presets = CreatePresets();
        public string DefaultPreset = "Default";
        public int RenderOrderMinimum = -16000;
        public int RenderOrderMaximum = 16000;
        public int DefaultPageOrderSpan = 32;
        public int PreferredOrderGap = 32;
        public int PreferredLayerGap = 128;
        public int CacheCapacity = 16;
        public int QueueCapacity = 64;
        public int PreloadCapacity = 32;
        public int CleanupCapacity = 16;
        public int TerminalCapacity = 256;

        /// <summary>无项目配置时使用的内置预设名称，与内置规则共用同一来源。</summary>
        public static IReadOnlyList<string> BuiltInPresetNames => CreatePresets().ConvertAll(item => item.Name).AsReadOnly();

        public RenderOrderOptions CreateRenderOrderOptions()
        {
            var gaps = new Dictionary<int, int>();
            if (Layers != null)
            {
                foreach (var entry in Layers)
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
            return new RenderOrderOptions(RenderOrderMinimum, RenderOrderMaximum, DefaultPageOrderSpan, PreferredOrderGap, 0, gaps, PreferredLayerGap);
        }

        /// <summary>复制项目默认、预设及页面覆盖，返回不再引用配置资产的运行策略。</summary>
        public PagePolicy ResolvePolicy(string preset = null, string layer = null, PagePolicyOverrides overrides = null)
        {
            RequireValid();
            var policy = Resolve(Layers, DefaultRules, Presets, preset ?? DefaultPreset, layer, overrides, Name);
            RequireRenderSpan(policy);
            return AttachPagePolicies(policy, preset ?? DefaultPreset, layer, overrides);
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
                CreateRenderOrderOptions();
            }
            catch (Exception error)
            {
                errors.Add(error.Message);
            }
            var layerNames = new HashSet<string>(StringComparer.Ordinal);
            var layerOrders = new HashSet<int>();
            if (Layers == null || Layers.Count == 0)
            {
                errors.Add("至少需要一个命名层级。");
            }
            else
            {
                foreach (var entry in Layers)
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
            if (Presets == null || Presets.Count == 0)
            {
                errors.Add("至少需要一个策略预设。");
            }
            else
            {
                foreach (var entry in Presets)
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

            if (string.IsNullOrWhiteSpace(DefaultPreset) || !presetNames.Contains(DefaultPreset))
            {
                errors.Add("默认策略必须指向已声明的预设。");
            }
            if (CacheCapacity < 0 || QueueCapacity < 1 || PreloadCapacity < 1 || CleanupCapacity < 1 || TerminalCapacity < 1)
            {
                errors.Add("缓存容量必须非负；队列、预加载、清理及终态记录容量必须大于零。");
            }
            if (errors.Count == 0)
            {
                try
                {
                    RequireRenderSpan(ResolveDefaults(Layers, DefaultRules));
                }
                catch (Exception error)
                {
                    errors.Add("默认规则：" + error.Message);
                }
                foreach (var entry in Presets)
                {
                    try
                    {
                        RequireRenderSpan(Resolve(Layers, DefaultRules, Presets, entry.Name, null, null, Name));
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

        /// <summary>未绑定资产时沿用内置预设；不依赖配置资产。</summary>
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

        public static List<MUILayer> CreateLayers() => new List<MUILayer>
        {
            new MUILayer("Background", -100), new MUILayer("Default", 0),
            new MUILayer("Popup", 100), new MUILayer("Notice", 200)
        };

        public static List<PagePolicyPreset> CreatePresets() => new List<PagePolicyPreset>
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
