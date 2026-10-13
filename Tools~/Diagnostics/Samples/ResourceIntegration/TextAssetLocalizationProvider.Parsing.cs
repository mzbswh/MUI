using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MUI.Samples.ResourceIntegration
{
    public sealed partial class TextAssetLocalizationProvider
    {
        /// <summary>两种加载模式共用同一解析与校验，返回目录不再引用源 TextAsset。</summary>
        private LocalizationCatalog Parse(TextAsset source, string normalized)
        {
            if (source == null)
            {
                throw new InvalidOperationException("本地化 TextAsset 不存在。");
            }

            var json = source.text;
            if (json.Length > maxCharacters)
            {
                throw new InvalidOperationException("Localization JSON exceeds its character limit.");
            }

            var data = JsonUtility.FromJson<CatalogData>(json);
            if (data == null || string.IsNullOrWhiteSpace(data.locale) || data.entries == null)
            {
                throw new FormatException("Localization JSON requires locale and entries.");
            }

            if (!string.Equals(new CultureInfo(data.locale, false).Name, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("Localization JSON locale does not match the requested locale.");
            }

            if (data.entries.Length > maxEntries)
            {
                throw new FormatException("Localization entry capacity exceeded.");
            }

            var entries = new Dictionary<string, Translation>(StringComparer.Ordinal);
            foreach (var entry in data.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    throw new FormatException("Localization entry key is missing.");
                }

                if (entry.forms == null || entry.forms.Length == 0)
                {
                    if (entry.text == null)
                    {
                        throw new FormatException("Localization entry requires text or plural forms.");
                    }

                    entries.Add(entry.key, new Translation(entry.text));
                    continue;
                }

                if (entry.text != null)
                {
                    throw new FormatException("Use either text or plural forms for an entry.");
                }

                var forms = new Dictionary<PluralCategory, string>();
                foreach (var form in entry.forms)
                {
                    if (form == null || !Enum.TryParse(form.category, true, out PluralCategory category) || !Enum.IsDefined(typeof(PluralCategory), category) || !string.Equals(category.ToString(), form.category, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new FormatException("Unknown plural category.");
                    }

                    forms.Add(category, form.text);
                }

                entries.Add(entry.key, new Translation(forms));
            }

            var rule = pluralRule;
            return new LocalizationCatalog(normalized, entries, count => rule(normalized, count), fallback == null ? null : fallback(normalized));
        }

        [Serializable]
        private sealed class CatalogData
        {
            public string locale = null;
            public EntryData[] entries = null;
        }

        [Serializable]
        private sealed class EntryData
        {
            public string key = null;
            public string text = null;
            public FormData[] forms = null;
        }

        [Serializable]
        private sealed class FormData
        {
            public string category = null;
            public string text = null;
        }
    }
}
