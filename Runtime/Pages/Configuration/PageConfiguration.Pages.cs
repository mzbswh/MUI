using System;
using System.Collections.Generic;
using MUI.Resources;

namespace MUI.Pages
{
    public sealed partial class PageConfiguration
    {
        public List<PagePolicyEntry> PagePolicies = new List<PagePolicyEntry>();

        private PagePolicy AttachPagePolicies(PagePolicy fallback, string preset, string layer, PagePolicyOverrides overrides)
        {
            if (PagePolicies == null || PagePolicies.Count == 0)
                return fallback;
            var resolved = new Dictionary<ViewResource, PagePolicy>();
            foreach (var page in PagePolicies)
            {
                var policy = ResolvePageEntry(page, preset, layer);
                RequireRenderSpan(policy);
                resolved.Add(new ViewResource(page.ResourceKey, page.ResourceVersion), policy);
            }
            return fallback.WithResourcePolicies(resolved, overrides);
        }

        private PagePolicy ResolvePageEntry(PagePolicyEntry page, string preset, string layer)
        {
            if (page.Values == null || (page.Overrides & ~PagePolicyFields.All) != 0)
                throw new ArgumentException("Invalid page policy override: " + page.ResourceKey);
            var policy = Resolve(Layers, DefaultRules, Presets,
                string.IsNullOrEmpty(page.Preset) ? preset : page.Preset, null, null, Name);
            var pageLayer = (page.Overrides & PagePolicyFields.Layer) != 0
                ? FindLayer(Layers, page.Values.Layer).Order : policy.Layer;
            policy = page.Values.CreateOverrides(page.Overrides, pageLayer).Apply(policy,
                (page.Overrides & PagePolicyFields.Layer) != 0 ? page.Values.Layer : null);
            if (layer != null)
                policy = new PagePolicyOverrides { Layer = FindLayer(Layers, layer).Order }.Apply(policy, layer);
            return policy;
        }

        private void ValidatePagePolicies(List<string> errors)
        {
            if (PagePolicies == null)
                return;
            var resources = new HashSet<ViewResource>();
            foreach (var page in PagePolicies)
            {
                try
                {
                    if (page == null)
                        throw new ArgumentException("Page settings cannot be null.");
                    var resource = new ViewResource(page.ResourceKey, page.ResourceVersion);
                    if (!resources.Add(resource))
                        throw new ArgumentException("Duplicate page settings: " + resource);
                    RequireRenderSpan(ResolvePageEntry(page, DefaultPreset, null));
                }
                catch (Exception error) { errors.Add("页面配置：" + error.Message); }
            }
        }
    }
}
