using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    /// <summary>缓存唯一持有已重置 View 的取得凭证及兼容代际，不保存业务生命周期对象。</summary>
    internal sealed class CachedViewContent : ViewContent
    {
        private IAcquiredView acquisition;

        internal CachedViewContent(IAcquiredView acquisition, object cacheGeneration,
            object bindingGeneration, object providerVersion) : base(cacheGeneration, bindingGeneration, providerVersion)
        {
            this.acquisition = acquisition;
        }

        internal bool IsAlive => acquisition != null && acquisition.View != null && acquisition.View.IsAlive;

        internal void PrepareForReuse() => ((ICacheableViewAcquisition)acquisition).PrepareForReuse();

        /// <summary>移出目录后唯一接管凭证；不得在缓存和新实例中同时持有归还责任。</summary>
        internal IAcquiredView TakeAcquisition()
        {
            var saved = acquisition ?? throw new InvalidOperationException("Cached view acquisition has already been transferred.");
            acquisition = null;
            return saved;
        }

        internal override async ValueTask ReleaseCachedAsync(List<Exception> errors,
            Func<IViewResourceReleaseTraceScope> beginResourceRelease = null)
        {
            var saved = acquisition;
            acquisition = null;
            using (var phase = beginResourceRelease == null ? null : beginResourceRelease())
            {
                var before = errors.Count;
                await ViewInstanceCleanup.ReleaseViewResourceAsync(saved, errors);
                if (phase != null)
                {
                    if (errors.Count == before)
                    {
                        phase.Complete();
                    }
                    else
                    {
                        phase.Fail(errors[before]);
                    }
                }
            }
        }
    }
}
