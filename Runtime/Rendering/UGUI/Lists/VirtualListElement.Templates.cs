using System;
using System.Collections.Generic;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class VirtualListElement
    {
        [SerializeField]
        private VirtualListTemplate[] itemTemplates = Array.Empty<VirtualListTemplate>();
        private readonly Dictionary<string, View> templatesByKey = new Dictionary<string, View>(StringComparer.Ordinal);

        /// <summary>配置命名模板；条目未指定 TemplateKey 时继续使用默认模板。</summary>
        public void ConfigureTemplates(IEnumerable<VirtualListTemplate> templates)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Configure item templates before initialization.");
            }

            if (templates == null)
            {
                throw new ArgumentNullException(nameof(templates));
            }

            var copied = new List<VirtualListTemplate>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                if (template == null || string.IsNullOrWhiteSpace(template.Key) || template.View == null || !keys.Add(template.Key))
                {
                    throw new ArgumentException("Item templates require unique non-empty keys and valid views.", nameof(templates));
                }

                copied.Add(template);
            }

            itemTemplates = copied.ToArray();
        }

        private void InitializeTemplates()
        {
            templatesByKey.Clear();
            foreach (var template in itemTemplates ?? Array.Empty<VirtualListTemplate>())
            {
                if (template == null || string.IsNullOrWhiteSpace(template.Key) || template.View == null || templatesByKey.ContainsKey(template.Key))
                {
                    throw new InvalidOperationException("Item templates require unique non-empty keys and valid views.");
                }

                var view = template.View;
                if (!(view.transform is RectTransform) || view.gameObject.activeSelf ||
                    !view.transform.IsChildOf(transform) || view.transform.IsChildOf(scrollRect.content))
                {
                    throw new InvalidOperationException("Named templates must be inactive RectTransform views inside the boundary and outside content.");
                }

                templatesByKey.Add(template.Key, view);
            }

            ValidateItemTemplates(snapshot);
        }

        private View ResolveItemTemplate(string key)
        {
            if (key == null)
            {
                return itemTemplate;
            }

            if (!templatesByKey.TryGetValue(key, out var template) || template == null)
            {
                throw new InvalidOperationException("Virtual list item references an unknown or destroyed template: " + key);
            }

            return template;
        }

        private void ValidateItemTemplates(IEnumerable<VirtualListItem> source)
        {
            foreach (var item in source)
            {
                ResolveItemTemplate(item.TemplateKey);
            }
        }
    }
}
