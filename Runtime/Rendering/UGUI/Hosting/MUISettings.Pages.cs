using System;
using System.Collections.Generic;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    [Serializable]
    public sealed class MUIPageSettings
    {
        public string ResourceKey;
        public string ResourceVersion = "1";
        public string PrefabGuid;
        public string PrefabPath;
        public bool ResourceKeyFromPath;
        public string Preset;
        public MUIPagePolicyFields Overrides;
        public MUIPagePolicyValues Values = new MUIPagePolicyValues();
    }

    public sealed partial class MUISettings
    {
        [SerializeField] private List<MUIPageSettings> pagePolicies = new List<MUIPageSettings>();

        private RoutePolicy AttachPagePolicies(RoutePolicy fallback, string preset, string layer, RoutePolicyOverrides overrides)
        {
            if (pagePolicies == null || pagePolicies.Count == 0) return fallback;
            var resolved = new Dictionary<ViewResource, RoutePolicy>();
            foreach (var page in pagePolicies)
            {
                var policy = ResolvePageEntry(page, preset, layer);
                RequireRenderSpan(policy);
                resolved.Add(new ViewResource(page.ResourceKey, page.ResourceVersion), policy);
            }
            return fallback.WithResourcePolicies(resolved, overrides);
        }

        private RoutePolicy ResolvePageEntry(MUIPageSettings page, string preset, string layer)
        {
            if (page.Values == null || (page.Overrides & ~MUIPagePolicyFields.All) != 0)
                throw new ArgumentException("Invalid page policy override: " + page.ResourceKey);
            var policy = Resolve(layers, defaultRules, presets,
                string.IsNullOrEmpty(page.Preset) ? preset : page.Preset, null, null, name);
            var pageLayer = (page.Overrides & MUIPagePolicyFields.Layer) != 0
                ? FindLayer(layers, page.Values.Layer).Order : policy.Layer;
            policy = page.Values.CreateOverrides(page.Overrides, pageLayer).Apply(policy,
                (page.Overrides & MUIPagePolicyFields.Layer) != 0 ? page.Values.Layer : null);
            if (layer != null)
                policy = new RoutePolicyOverrides { Layer = FindLayer(layers, layer).Order }.Apply(policy, layer);
            return policy;
        }

        private void ValidatePagePolicies(List<string> errors)
        {
            if (pagePolicies == null) return;
            var resources = new HashSet<ViewResource>();
            foreach (var page in pagePolicies)
            {
                try
                {
                    if (page == null) throw new ArgumentException("Page settings cannot be null.");
                    var resource = new ViewResource(page.ResourceKey, page.ResourceVersion);
                    if (!resources.Add(resource)) throw new ArgumentException("Duplicate page settings: " + resource);
                    RequireRenderSpan(ResolvePageEntry(page, defaultPreset, null));
                }
                catch (Exception error) { errors.Add("页面配置：" + error.Message); }
            }
        }
    }
}
