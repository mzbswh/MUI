using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>预先登记内存语言目录，每次加载交出独立凭证；释放不销毁共享的纯文本目录。</summary>
    public sealed class InMemoryLocalizationProvider : ILocalizationProvider
    {
        private readonly Dictionary<string, LocalizationCatalog> catalogs =
            new Dictionary<string, LocalizationCatalog>(StringComparer.OrdinalIgnoreCase);

        /// <summary>复制语言索引，拒绝空目录与重复语言；后续不修改此目录集合。</summary>
        public InMemoryLocalizationProvider(IEnumerable<LocalizationCatalog> catalogs)
        {
            if (catalogs == null)
            {
                throw new ArgumentNullException(nameof(catalogs));
            }
            foreach (var catalog in catalogs)
            {
                if (catalog == null)
                {
                    throw new ArgumentException("语言目录不能为空。", nameof(catalogs));
                }
                this.catalogs.Add(catalog.Locale, catalog);
            }
        }

        /// <summary>立即交付独立凭证，已取消的请求不取得目录持有权。</summary>
        public ValueTask<IAcquiredResource<LocalizationCatalog>> LoadAsync(string locale, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IAcquiredResource<LocalizationCatalog>>(
                new AcquiredResource<LocalizationCatalog>(Find(locale), released => default));
        }

        private LocalizationCatalog Find(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                throw new ArgumentException("语言名不能为空。", nameof(locale));
            }
            var normalized = new CultureInfo(locale, false).Name;
            if (!catalogs.TryGetValue(normalized, out var catalog))
            {
                throw new KeyNotFoundException($"语言 '{normalized}' 未登记。");
            }
            return catalog;
        }
    }
}
