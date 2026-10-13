using System;
using System.Collections.Generic;
using System.Globalization;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>复制并保存一个文本键的复数形式；必须包含 Other 作为类别回退。</summary>
    public sealed class Translation
    {
        private readonly Dictionary<PluralCategory, string> forms;

        public Translation(string text) : this(new Dictionary<PluralCategory, string> { [PluralCategory.Other] = text })
        {
        }

        public Translation(IEnumerable<KeyValuePair<PluralCategory, string>> forms)
        {
            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            this.forms = new Dictionary<PluralCategory, string>();
            foreach (var form in forms)
            {
                if (!Enum.IsDefined(typeof(PluralCategory), form.Key))
                {
                    throw new ArgumentException("Unknown plural category.", nameof(forms));
                }

                if (form.Value == null)
                {
                    throw new ArgumentException("Translation cannot be null.", nameof(forms));
                }

                this.forms.Add(form.Key, form.Value);
            }

            if (!this.forms.ContainsKey(PluralCategory.Other))
            {
                throw new ArgumentException("An Other form is required.", nameof(forms));
            }
        }

        internal string Get(PluralCategory category) => forms.TryGetValue(category, out var value) ? value : forms[PluralCategory.Other];
    }

    /// <summary>复制文本目录并使用只读文化信息；复数规则由项目显式提供，不从语言前缀猜测。</summary>
    public sealed class LocalizationCatalog
    {
        private readonly Dictionary<string, Translation> entries;
        private readonly Func<decimal, PluralCategory> pluralRule;
        private readonly LocalizationCatalog fallback;

        /// <summary>建立目录与可选回退链；缺键时查找回退目录，缺复数形式时使用 Other。</summary>
        public LocalizationCatalog(string locale, IEnumerable<KeyValuePair<string, Translation>> entries,
            Func<decimal, PluralCategory> pluralRule, LocalizationCatalog fallback = null)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                throw new ArgumentException("Locale is required.", nameof(locale));
            }

            Culture = CultureInfo.ReadOnly(new CultureInfo(locale, false));
            Locale = Culture.Name;
            this.pluralRule = pluralRule ?? throw new ArgumentNullException(nameof(pluralRule));
            this.fallback = fallback;
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            this.entries = new Dictionary<string, Translation>(StringComparer.Ordinal);
            foreach (var pair in entries)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    throw new ArgumentException("Translation key and entry are required.", nameof(entries));
                }

                this.entries.Add(pair.Key, pair.Value);
            }
        }

        public string Locale
        {
            get;
        }

        public CultureInfo Culture
        {
            get;
        }

        public bool IsRightToLeft => Culture.TextInfo.IsRightToLeft;

        /// <summary>查找译文并格式化；目录、回退链均缺少该键时抛出 KeyNotFoundException。</summary>
        public LocalizedTextValue Format(LocalizedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var catalog = this;
            while (catalog != null)
            {
                if (catalog.entries.TryGetValue(message.Key, out var entry))
                {
                    // 回退译文采用自己的语法类别，但参数数字和日期仍按当前选中的语言格式化。
                    var category = message.Count.HasValue ? catalog.pluralRule(message.Count.Value) : PluralCategory.Other;
                    if (!Enum.IsDefined(typeof(PluralCategory), category))
                    {
                        throw new InvalidOperationException("Plural rule returned an invalid category.");
                    }

                    return new LocalizedTextValue(message.Format(entry.Get(category), Culture), Locale, catalog.Locale, catalog.IsRightToLeft);
                }

                catalog = catalog.fallback;
            }

            throw new KeyNotFoundException($"Translation '{message.Key}' is missing in '{Locale}' and its fallback catalogs.");
        }
    }
}
