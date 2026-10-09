using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>
    /// 将界面提供方返回的预加载凭证接入生命周期。
    /// 此扩展只负责取消、代际校验和凭证托管，不实现资源加载、缓存、下载或物理卸载。
    /// </summary>
    public static class LifetimePreloadExtensions
    {
        /// <summary>保持预加载驻留直到此生命周期结束，不创建 UI 实例。</summary>
        public static ValueTask<IAcquiredPreload> PreloadAsync(this LifetimeScope lifetime, IPreloadViewProvider provider, ViewResource resource, CancellationToken cancellationToken = default)
        {
            if (lifetime == null)
            {
                throw new ArgumentNullException(nameof(lifetime));
            }

            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return lifetime.RunAsync(token => PreloadOwnedAsync(lifetime, provider, resource, token, cancellationToken));
        }

        private static async ValueTask<IAcquiredPreload> PreloadOwnedAsync(LifetimeScope lifetime, IPreloadViewProvider provider, ViewResource resource, CancellationToken owner, CancellationToken caller)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(owner, caller))
            {
                IAcquiredPreload ownedResource = null;
                try
                {
                    ownedResource = await provider.PreloadAsync(resource, linked.Token);
                    if (ownedResource == null)
                    {
                        throw new InvalidOperationException("Preload provider returned no resource acquisition.");
                    }

                    linked.Token.ThrowIfCancellationRequested();
                    if (!resource.Equals(ownedResource.Resource))
                    {
                        throw new InvalidOperationException("Preload resource identity mismatch.");
                    }

                    lifetime.Own(ownedResource);
                    var result = ownedResource;
                    ownedResource = null;
                    return result;
                }
                catch (Exception failure)
                {
                    if (ownedResource != null)
                    {
                        try
                        {
                            await CleanupRegistry.ReleaseAsync(ownedResource, "LifetimeScope.LatePreload", lifetime);
                        }
                        catch (Exception cleanup)
                        {
                            throw new AggregateException("Preload and late-result cleanup failed.", failure, cleanup);
                        }
                    }
                    else
                    {
                        try
                        {
                            await ResourceLoadCleanup.AwaitAsync(failure, lifetime);
                        }
                        catch (Exception cleanup)
                        {
                            throw new AggregateException("预加载与后端回滚均失败。", failure, cleanup);
                        }
                    }

                    if (ResourceLoadCleanup.Cause(failure) is OperationCanceledException cancelled)
                    {
                        throw new OperationCanceledException("预加载已取消，回滚已结束。", failure, cancelled.CancellationToken);
                    }

                    if (failure is ObjectDisposedException && lifetime.IsEnded)
                    {
                        throw new OperationCanceledException("Preload lifetime ended.", failure, owner);
                    }

                    throw;
                }
            }
        }
    }
}
