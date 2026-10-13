using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>通过统一资源加载器读取 JSON TextAsset，复制文本目录并归还源资源后交付目录。</summary>
    public sealed partial class TextAssetLocalizationProvider : ILocalizationProvider
    {
        private readonly IResourceLoader loader;
        private readonly Func<string, string> resolveKey;
        private readonly Func<string, decimal, PluralCategory> pluralRule;
        private readonly Func<string, LocalizationCatalog> fallback;
        private readonly int maxEntries;
        private readonly int maxCharacters;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;

        public TextAssetLocalizationProvider(IResourceLoader loader,
                    Func<string, string> resolveKey,
                    Func<string, decimal, PluralCategory> pluralRule,
                    Func<string, LocalizationCatalog> fallback = null,
                    int maxEntries = 10000,
                    int maxCharacters = 2000000)
                    : this(resolveKey, pluralRule, fallback, maxEntries, maxCharacters)
        {
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        private TextAssetLocalizationProvider(Func<string, string> resolveKey,
                    Func<string, decimal, PluralCategory> pluralRule,
                    Func<string, LocalizationCatalog> fallback, int maxEntries, int maxCharacters)
        {
            this.resolveKey = resolveKey ?? throw new ArgumentNullException(nameof(resolveKey));
            this.pluralRule = pluralRule ?? throw new ArgumentNullException(nameof(pluralRule));
            if (maxEntries < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            }

            if (maxCharacters < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCharacters));
            }

            this.fallback = fallback;
            this.maxEntries = maxEntries;
            this.maxCharacters = maxCharacters;
        }

        public ValueTask<IAcquiredResource<LocalizationCatalog>> LoadAsync(string locale, CancellationToken cancellationToken)
        {
            RequireThread();
            cancellationToken.ThrowIfCancellationRequested();
            var key = Resolve(locale, out var normalized);
            return LoadCoreAsync(normalized, key, cancellationToken);
        }

        private string Resolve(string locale, out string normalized)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                throw new ArgumentException("Locale is required.", nameof(locale));
            }

            normalized = new CultureInfo(locale, false).Name;
            var key = resolveKey(normalized);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Localization key resolver returned no key.");
            }

            return key;
        }

        private async ValueTask<IAcquiredResource<LocalizationCatalog>> LoadCoreAsync(
                    string normalized, string key, CancellationToken cancellationToken)
        {
            IAcquiredResource<TextAsset> source = null;
            LocalizationCatalog catalog;
            try
            {
                source = await loader.LoadAsync<TextAsset>(key, cancellationToken);
                RequireThread();
                cancellationToken.ThrowIfCancellationRequested();
                if (source == null)
                {
                    throw new InvalidOperationException("Localization TextAsset is missing.");
                }

                catalog = Parse(source.Asset, normalized);
            }
            catch (Exception failure)
            {
                if (source != null)
                {
                    try
                    {
                        await source.DisposeAsync();
                    }
                    catch (Exception cleanup)
                    {
                        throw new AggregateException(failure, cleanup);
                    }
                }

                throw;
            }

            await source.DisposeAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return new AcquiredResource<LocalizationCatalog>(catalog, released => default);
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("TextAsset localization requires its owning Unity thread and SynchronizationContext.");
            }
        }
    }
}
