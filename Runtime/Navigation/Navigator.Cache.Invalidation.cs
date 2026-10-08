using System;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private object cacheGeneration = new object();

        internal object CacheGeneration => cacheGeneration;

        internal object CaptureProviderVersion()
        {
            using (EnterCallback(null))
            {
                var source = provider;
                var version = source is IViewContentVersion versioned ? versioned.ContentVersion : source;
                if (version == null || version.GetType().IsValueType)
                {
                    throw new InvalidOperationException("Provider content version must be a non-null reference token.");
                }

                return version;
            }
        }

        internal bool IsProviderVersionCurrent(ViewContent content) =>
            ReferenceEquals(content.ProviderVersion, CaptureProviderVersion());

        private bool IsCacheCompatible(ViewContent content, object providerVersion) =>
            ReferenceEquals(content.CacheGeneration, cacheGeneration) &&
            ReferenceEquals(content.BindingGeneration, BindingRegistry.Generation) &&
            ReferenceEquals(content.ProviderVersion, providerVersion);

        /// <summary>
        /// 使当前实例内容失去后续缓存资格，并清理已有缓存及在途淘汰。
        /// 不结束活动页面或取消打开请求；上下文切换仍须由项目协调活动业务与资源提供方。
        /// </summary>
        private ValueTask InvalidateCacheAsyncUntraced()
        {
            AssertThread();
            if (IsReentrant || IsSourceCommandRunning || HasCloseEvaluation)
            {
                throw new InvalidOperationException("Cannot invalidate cache from navigation callbacks.");
            }

            // 先同步切换代际再异步释放，迟到的旧页面关闭不能污染新缓存。
            cacheGeneration = new object();
            return new ValueTask(BeginCacheClear());
        }
    }
}
