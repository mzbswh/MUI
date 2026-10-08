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

            await ReleaseViewResourceAsync(viewResource, errors);
        }

        /// <summary>归还已结束生命周期的视图凭证；失败加入现有清理结果。</summary>
        internal static async ValueTask ReleaseViewResourceAsync(IAsyncDisposable viewResource, List<Exception> errors)
        {
            if (viewResource != null)
            {
                try
                {
                    await viewResource.DisposeAsync();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
        }
    }
}
