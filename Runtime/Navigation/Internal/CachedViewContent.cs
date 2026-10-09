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
        private CleanupResponsibility cleanupResponsibility;
        private bool cleanupConfirmed;

        internal CachedViewContent(IAcquiredView acquisition, object cacheGeneration,
            object bindingGeneration, object providerVersion) : base(cacheGeneration, bindingGeneration, providerVersion)
        {
            this.acquisition = acquisition;
        }

        internal bool IsAlive => acquisition != null && acquisition.View != null && acquisition.View.IsAlive;

        /// <summary>仅观察已捕获的框架责任；不重读提供方属性，也不重放首次归还。</summary>
        internal bool IsCleanupConfirmed => cleanupConfirmed ||
            (cleanupResponsibility != null && cleanupResponsibility.CaptureSnapshot().State == CleanupResponsibilityState.Completed);

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
                IAsyncDisposable release = saved;
                if (saved != null)
                {
                    try
                    {
                        // 捕获稳定责任后再归还，预算可以观察显式叶重试的确认而不访问项目状态。
                        cleanupResponsibility = CleanupRegistry.GetResponsibility(saved, "CachedViewContent.ViewResource");
                        if (cleanupResponsibility == null)
                        {
                            throw new InvalidOperationException("Cached view cleanup adapter returned no responsibility.");
                        }
                    }
                    catch (Exception error)
                    {
                        // 属性失效仍执行真实归还一次；失败由回退责任持有对象和回调，不声明可重试。
                        errors.Add(error);
                        cleanupResponsibility = new CleanupResponsibility(saved.DisposeAsync, "CachedViewContent.ViewResource");
                        release = cleanupResponsibility;
                    }
                }
                var beforeRelease = errors.Count;
                await ViewInstanceCleanup.ReleaseViewResourceAsync(release, errors);
                cleanupConfirmed = errors.Count == beforeRelease;
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
