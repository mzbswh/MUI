using System;
using MUI.Localization;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.ResourceIntegration
{
    public sealed partial class TextAssetLocalizationProvider
    {
        private readonly ISynchronousResourceLoader synchronousLoader;

        private TextAssetLocalizationProvider(ISynchronousResourceLoader loader,
                    Func<string, string> resolveKey,
                    Func<string, decimal, PluralCategory> pluralRule,
                    Func<string, LocalizationCatalog> fallback, int maxEntries, int maxCharacters)
                    : this(resolveKey, pluralRule, fallback, maxEntries, maxCharacters)
        {
            synchronousLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        /// <summary>由构造入口确定能力；提供方不会按某次加载结果临时切换模式。</summary>
        public LifetimeMode Mode => synchronousLoader == null ? LifetimeMode.AsyncAllowed : LifetimeMode.Synchronous;

        /// <summary>只接收同步资源契约，不要求后端实现异步方法；后端所有权仍归调用方。</summary>
        public static TextAssetLocalizationProvider CreateSynchronous(ISynchronousResourceLoader loader,
            Func<string, string> resolveKey,
            Func<string, decimal, PluralCategory> pluralRule,
            Func<string, LocalizationCatalog> fallback = null,
            int maxEntries = 10000,
            int maxCharacters = 2000000) =>
            new TextAssetLocalizationProvider(loader, resolveKey, pluralRule, fallback, maxEntries, maxCharacters);

        /// <summary>
        /// 同步读取并复制 JSON 目录，归还源 TextAsset 后才交出目录凭证。
        /// 源归还失败时不发布候选，并报告无法确认完成的清理，不启动后台补偿。
        /// </summary>
        public ISynchronousResourceLease<LocalizationCatalog> Load(string locale)
        {
            RequireThread();
            if (Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("此本地化提供方使用异步模式，请调用 LoadAsync。");
            }

            var key = Resolve(locale, out var normalized);
            ISynchronousResourceLease<TextAsset> source = null;
            LocalizationCatalog catalog;
            try
            {
                source = synchronousLoader.Load<TextAsset>(key);
                if (source == null)
                {
                    throw new InvalidOperationException("本地化加载器未返回 TextAsset 凭证。");
                }
                catalog = Parse(source.Asset, normalized);
            }
            catch (Exception failure)
            {
                if (source != null)
                {
                    ReleaseSource(source, failure);
                }
                throw;
            }

            ReleaseSource(source, null);
            return new SynchronousResourceLease<LocalizationCatalog>(catalog, released => { });
        }

        private static void ReleaseSource(ISynchronousResourceLease<TextAsset> source, Exception loadError)
        {
            try
            {
                source.Dispose();
            }
            catch (Exception cleanup)
            {
                // 即使解析成功，源资源未确认归还也属于加载失败，而不是有效目录交付。
                throw new SynchronousResourceLoadException(loadError ??
                    new InvalidOperationException("语言目录解析成功，但源 TextAsset 未能归还。"), cleanup);
            }
        }
    }
}
