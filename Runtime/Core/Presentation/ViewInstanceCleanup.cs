using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>父子视图共用的实例资源清理。Presenter 销毁回调应在调用前完成。</summary>
    internal static class ViewInstanceCleanup
    {
        /// <summary>
        /// 先排空实例资源，再分别移除宿主监听，最后归还视图资源。
        /// 使用标准异步释放契约，Core 不依赖具体 AcquiredView 或资源提供方。
        /// </summary>
        internal static async ValueTask RunAsync(
            LifetimeScope instance,
            IAsyncDisposable viewResource,
            List<Exception> errors,
            params Action[] detachListeners)
        {
            try
            {
                await instance.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            foreach (var detach in detachListeners)
            {
                try
                {
                    detach();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            await ReleaseViewResourceAsync(viewResource, errors, () => instance.IsCleanupConfirmed);
        }

        /// <summary>归还已结束生命周期的视图凭证；失败加入现有清理结果。</summary>
        internal static async ValueTask ReleaseViewResourceAsync(IAsyncDisposable viewResource, List<Exception> errors)
        {
            await ReleaseViewResourceAsync(viewResource, errors, null);
        }

        /// <summary>视图仍被未确认的实例清理依赖持有时，保留凭证至项目显式确认依赖后重试。</summary>
        internal static async ValueTask ReleaseViewResourceAsync(IAsyncDisposable viewResource,
            List<Exception> errors, Func<bool> dependenciesConfirmed)
        {
            if (viewResource != null)
            {
                try
                {
                    if (dependenciesConfirmed == null || dependenciesConfirmed())
                    {
                        await CleanupRegistry.ReleaseAsync(viewResource, "ViewInstance.ViewResource");
                    }
                    else
                    {
                        var acquisition = CleanupRegistry.GetResponsibility(viewResource, "ViewInstance.ViewResource");
                        var deferred = new CleanupResponsibility(async () =>
                        {
                            if (!dependenciesConfirmed())
                            {
                                throw new InvalidOperationException("View resource is retained until its cleanup dependencies are confirmed.");
                            }
                            var state = acquisition.CaptureSnapshot();
                            if (state.State == CleanupResponsibilityState.Completed)
                            {
                                return;
                            }
                            if (state.State == CleanupResponsibilityState.Failed)
                            {
                                throw state.Failure ?? new InvalidOperationException("View acquisition cleanup is unconfirmed.");
                            }
                            await acquisition.DisposeAsync();
                        }, "ViewInstance.DependencyRelease", true, System.Threading.Thread.CurrentThread.ManagedThreadId);
                        await deferred.DisposeAsync();
                    }
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
        }
    }
}
