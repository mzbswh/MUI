using System;
using System.Collections.Generic;
using MUI.Pages;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    [Serializable]
    public sealed class MUIPageSettings
    {
        public string ResourceKey;
        public string ResourceVersion = "1";
#if UNITY_EDITOR
        public string PrefabGuid;
        public string PrefabPath;
        public bool ResourceKeyFromPath;
#endif
        public string Preset;
        public PagePolicyFields Overrides;
        public PagePolicySettings Values = new PagePolicySettings();
    }

    public sealed partial class MUISettings
    {
        [SerializeField] private List<MUIPageSettings> pagePolicies = new List<MUIPageSettings>();

        private PagePolicy AttachPagePolicies(PagePolicy fallback, string preset, string layer, PagePolicyOverrides overrides)
        {
            if (pagePolicies == null || pagePolicies.Count == 0)
                return fallback;
            var resolved = new Dictionary<ViewResource, PagePolicy>();
            foreach (var page in pagePolicies)
            {
                var policy = ResolvePageEntry(page, preset, layer);
                RequireRenderSpan(policy);
                resolved.Add(new ViewResource(page.ResourceKey, page.ResourceVersion), policy);
            }
            return fallback.WithResourcePolicies(resolved, overrides);
        }

        private PagePolicy ResolvePageEntry(MUIPageSettings page, string preset, string layer)
        {
            if (page.Values == null || (page.Overrides & ~PagePolicyFields.All) != 0)
                throw new ArgumentException("Invalid page policy override: " + page.ResourceKey);
            var policy = Resolve(layers, defaultRules, presets,
                string.IsNullOrEmpty(page.Preset) ? preset : page.Preset, null, null, name);
            var pageLayer = (page.Overrides & PagePolicyFields.Layer) != 0
                ? FindLayer(layers, page.Values.Layer).Order : policy.Layer;
            policy = page.Values.CreateOverrides(page.Overrides, pageLayer).Apply(policy,
                (page.Overrides & PagePolicyFields.Layer) != 0 ? page.Values.Layer : null);
            if (layer != null)
                policy = new PagePolicyOverrides { Layer = FindLayer(layers, layer).Order }.Apply(policy, layer);
            return policy;
        }

        private void ValidatePagePolicies(List<string> errors)
        {
            if (pagePolicies == null)
                return;
            var resources = new HashSet<ViewResource>();
            foreach (var page in pagePolicies)
            {
                try
                {
                    if (page == null)
                        throw new ArgumentException("Page settings cannot be null.");
                    var resource = new ViewResource(page.ResourceKey, page.ResourceVersion);
                    if (!resources.Add(resource))
                        throw new ArgumentException("Duplicate page settings: " + resource);
                    RequireRenderSpan(ResolvePageEntry(page, defaultPreset, null));
                }
                catch (Exception error) { errors.Add("页面配置：" + error.Message); }
            }
        }
    }
}
