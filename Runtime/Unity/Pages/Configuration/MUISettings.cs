using System;
using System.Collections.Generic;
using MUI.Pages;
using UnityEngine;

namespace MUI.Unity
{
    /// <summary>Unity asset carrier for backend-independent page configuration.</summary>
    [CreateAssetMenu(menuName = "MUI/Page Settings", fileName = "MUISettings")]
    public partial class MUISettings : ScriptableObject
    {
        [SerializeField] private List<MUILayer> layers = PageConfiguration.CreateLayers();
        [SerializeField] private PagePolicySettings defaultRules = new PagePolicySettings();
        [SerializeField] private List<PagePolicyPreset> presets = PageConfiguration.CreatePresets();
        [SerializeField] private string defaultPreset = "Default";
        [SerializeField] private int renderOrderMinimum = -16000;
        [SerializeField] private int renderOrderMaximum = 16000;
        [SerializeField] private int defaultPageOrderSpan = 32;
        [SerializeField] private int preferredOrderGap = 32;
        [SerializeField] private int preferredLayerGap = 128;
        [SerializeField, Min(0)] private int cacheCapacity = 16;
        [SerializeField, Min(1)] private int queueCapacity = 64;
        [SerializeField, Min(1)] private int preloadCapacity = 32;
        [SerializeField, Min(1)] private int cleanupCapacity = 16;
        [SerializeField, Min(1)] private int terminalCapacity = 256;
        [SerializeField] private List<MUIPageSettings> pagePolicies = new List<MUIPageSettings>();
        public IReadOnlyList<MUILayer> Layers => layers == null ? (IReadOnlyList<MUILayer>)Array.Empty<MUILayer>() : layers.AsReadOnly();
        public IReadOnlyList<PagePolicyPreset> Presets => presets == null ? (IReadOnlyList<PagePolicyPreset>)Array.Empty<PagePolicyPreset>() : presets.AsReadOnly();
        public string DefaultPreset => defaultPreset;
        public static IReadOnlyList<string> BuiltInPresetNames => PageConfiguration.BuiltInPresetNames;
        public int CacheCapacity => cacheCapacity;
        public int QueueCapacity => queueCapacity;
        public int PreloadCapacity => preloadCapacity;
        public int CleanupCapacity => cleanupCapacity;
        public int TerminalCapacity => terminalCapacity;
        public PageConfiguration CreateConfiguration() => new PageConfiguration
        {
            Name = name,
            Layers = layers,
            DefaultRules = defaultRules,
            Presets = presets,
            DefaultPreset = defaultPreset,
            RenderOrderMinimum = renderOrderMinimum,
            RenderOrderMaximum = renderOrderMaximum,
            DefaultPageOrderSpan = defaultPageOrderSpan,
            PreferredOrderGap = preferredOrderGap,
            PreferredLayerGap = preferredLayerGap,
            CacheCapacity = cacheCapacity,
            QueueCapacity = queueCapacity,
            PreloadCapacity = preloadCapacity,
            CleanupCapacity = cleanupCapacity,
            TerminalCapacity = terminalCapacity,
            PagePolicies = pagePolicies == null ? null : new List<PagePolicyEntry>(pagePolicies)
        };

        public virtual RenderOrderOptions CreateRenderOrderOptions() => CreateConfiguration().CreateRenderOrderOptions();
        public PagePolicy ResolvePolicy(string preset = null, string layer = null, PagePolicyOverrides overrides = null)
            => CreateConfiguration().ResolvePolicy(preset, layer, overrides);
        public static PagePolicy ResolveBuiltInPolicy(string preset = null, string layer = null, PagePolicyOverrides overrides = null)
            => PageConfiguration.ResolveBuiltInPolicy(preset, layer, overrides);
        public virtual IReadOnlyList<string> Validate() => CreateConfiguration().Validate();
        public void RequireValid()
        {
            var errors = Validate();
            if (errors.Count != 0) throw new InvalidOperationException("MUI configuration is invalid:\n" + string.Join("\n", errors));
        }
    }

}
