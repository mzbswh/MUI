using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>已取得的视图及一次归还责任；重复释放观察同一清理结果。</summary>
    public sealed class AcquiredView : ICacheableViewAcquisition, ICleanupResponsibilitySource
    {
        private readonly AcquiredResource<IView> resource;
        private readonly Action<IView> prepareForReuse;

        public AcquiredView(IView view, Func<IView, ValueTask> release, int? releaseThreadId = null,
            bool supportsIdempotentRetry = false, string owner = null,
            bool supportsCaching = false, Action<IView> prepareForReuse = null)
        {
            resource = new AcquiredResource<IView>(view, release, releaseThreadId, supportsIdempotentRetry, owner ?? "AcquiredView");
            SupportsCaching = supportsCaching;
            this.prepareForReuse = prepareForReuse;
        }

        public IView View => resource.Asset;

        public CleanupResponsibility CleanupResponsibility => resource.CleanupResponsibility;

        /// <summary>显式声明归还能力及必要依赖可跨页面关闭保留，默认不移交缓存。</summary>
        public bool SupportsCaching
        {
            get;
        }

        public void PrepareForReuse()
        {
            if (!SupportsCaching)
            {
                throw new InvalidOperationException("View acquisition does not support caching.");
            }
            prepareForReuse?.Invoke(View);
        }

        public ValueTask DisposeAsync() => resource.DisposeAsync();
    }
}
