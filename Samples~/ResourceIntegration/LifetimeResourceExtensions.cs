using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>项目侧加载接线示例；框架生命周期只接收已取得的凭证，不决定资源加载策略。</summary>
    public static class LifetimeResourceExtensions
    {
        /// <summary>
        /// 加载资源并先将凭证交给生命周期托管，再返回资源对象。
        /// 已取消或结束的生命周期不接受迟到结果。调用方必须
        /// 在 await 后、写入 Unity UI 前再次检查激活令牌。
        /// 返回资源为借用对象，不能自行卸载、销毁或在生命周期结束后使用。
        /// </summary>
        public static ValueTask<T> LoadAsync<T>(this LifetimeScope lifetime, IResourceLoader loader, string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (lifetime == null)
            {
                throw new ArgumentNullException(nameof(lifetime));
            }

            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Resource key is required.", nameof(key));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return lifetime.RunAsync(token => LoadOwnedAsync<T>(lifetime, loader, key, token, cancellationToken));
        }

        private static async ValueTask<T> LoadOwnedAsync<T>(LifetimeScope lifetime, IResourceLoader loader, string key, CancellationToken lifetimeToken, CancellationToken callerToken)
                    where T : class
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken, callerToken))
            {
                IAcquiredResource<T> ownedResource = null;
                try
                {
                    ownedResource = await loader.LoadAsync<T>(key, linked.Token);
                    if (ownedResource == null)
                    {
                        throw new InvalidOperationException("Resource loader returned a null resource acquisition.");
                    }

                    linked.Token.ThrowIfCancellationRequested();
                    var asset = ownedResource.Asset;
                    if (asset == null)
                    {
                        throw new InvalidOperationException("Resource loader returned a null asset.");
                    }

                    lifetime.Own(ownedResource);
                    ownedResource = null; // 所有权已转移，此凭证仅由生命周期释放。
                    return asset;
                }
                catch (Exception failure)
                {
                    if (ownedResource != null)
                    {
                        try
                        {
                            await ownedResource.DisposeAsync();
                        }
                        catch (Exception cleanupFailure)
                        {
                            lifetime.RecordCleanupFailure(cleanupFailure);
                            throw new AggregateException("Resource load and late-result cleanup failed.", failure, cleanupFailure);
                        }
                    }
                    else
                    {
                        try
                        {
                            await AwaitCleanupAsync(failure);
                        }
                        catch (Exception cleanupFailure)
                        {
                            lifetime.RecordCleanupFailure(cleanupFailure);
                            throw new AggregateException("资源加载与后端回滚均失败。", failure, cleanupFailure);
                        }
                    }

                    if (GetCause(failure) is OperationCanceledException cancelled)
                    {
                        throw new OperationCanceledException("资源加载已取消，回滚已结束。", failure, cancelled.CancellationToken);
                    }

                    // 首次令牌检查后，Own 仍可能与 Cancel 发生竞态。
                    if (failure is ObjectDisposedException && lifetime.IsEnded)
                    {
                        throw new OperationCanceledException("Resource lifetime ended.", failure, lifetimeToken);
                    }

                    throw;
                }
            }
        }

        // 后端回滚归加载操作负责；未取得凭证时仍需等待其清理完成。
        private static async ValueTask AwaitCleanupAsync(Exception failure)
        {
            if (failure is ResourceLoadException pending)
            {
                await pending.CleanupCompletion;
            }
        }

        private static Exception GetCause(Exception failure)
        {
            while (failure is ResourceLoadException && failure.InnerException != null)
            {
                failure = failure.InnerException;
            }

            return failure;
        }
    }
}
