using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>
    /// 将项目异步资源加载器适配为 UGUI Prefab 创建器。
    /// 预加载和界面凭证各自持有 Prefab；已驻留时立即返回。
    /// 创建失败的原生销毁与异步归还由提供方跟踪到退出。
    /// </summary>
    public sealed partial class LoadedPrefabViewProvider : IPreloadViewProvider, IVersionedViewProvider, IAsyncDisposable
    {
        private IResourceLoader loader;
        private Func<ViewResource, string> resolveKey;
        private readonly Transform parent;
        private Action<View> configureView;
        private readonly GameObject staging;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly int loadCapacity;
        private readonly List<Entry> resident = new List<Entry>();
        private readonly LifetimeScope loading = new LifetimeScope();
        private TaskCompletionSource<bool> disposal;
        private object contentVersion = new object();

        /// <summary>
        /// configureView 在每个新实例激活前同步配置 View；缓存复用不重复调用。
        /// 回调不能激活、销毁、重新挂载实例或启动异步工作，配置失败沿用创建回滚。
        /// </summary>
        public LoadedPrefabViewProvider(Transform parent, IResourceLoader loader, Func<ViewResource, string> resolveKey,
            int loadCapacity = 16, Action<View> configureView = null)
        {
            UnityMainThread.Require();
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (loadCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(loadCapacity));
            }

            this.parent = parent;
            this.configureView = configureView;
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.resolveKey = resolveKey ?? throw new ArgumentNullException(nameof(resolveKey));
            this.loadCapacity = loadCapacity;
            staging = new GameObject("MUI Loaded Prefab Staging", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(parent, false);
        }

        /// <summary>读取不加载资源；提供方关闭后仍可读，以便持有者识别旧缓存已失效。</summary>
        public object ContentVersion
        {
            get
            {
                RequireThread();
                return contentVersion;
            }
        }

        /// <summary>
        /// 项目更新加载器或资源键映射后在 Unity 线程调用。
        /// 旧持有权仍由原调用者释放，新请求不复用旧驻留项，旧加载结果不能发布。
        /// </summary>
        public void Invalidate()
        {
            RequireThread();
            if (disposal != null || parent == null || staging == null)
            {
                throw new ObjectDisposedException(nameof(LoadedPrefabViewProvider));
            }

            contentVersion = new object();
        }

        public bool IsResident(ViewResource resource)
        {
            RequireAlive(resource);
            return Find(resource) != null;
        }

        private IAcquiredView AcquireResident(ViewResource resource)
        {
            RequireAlive(resource);
            var entry = Find(resource);
            if (entry == null)
            {
                throw new InvalidOperationException("Resident view acquisition requires a retained prefab.");
            }

            return loading.Run(_ => CreateResidentView(entry));
        }

        private IAcquiredView CreateResidentView(Entry entry)
        {
            RequireCurrent(entry.Version);
            checked
            {
                ++entry.References;
            }
            try
            {
                return CreateView(entry);
            }
            catch (Exception failure)
            {
                throw new ResourceLoadException(failure, TrackFailedCreation(entry, failure));
            }
        }

        public Task<IAcquiredView> AcquireAsync(ViewResource resource, CancellationToken cancellationToken = default)
        {
            RequireAlive(resource);
            cancellationToken.ThrowIfCancellationRequested();
            var entry = Find(resource);
            if (entry != null)
            {
                return Task.FromResult<IAcquiredView>(AcquireResident(resource));
            }

            RequireCapacity();
            return loading.RunAsync(token => LoadViewAsync(resource, token, cancellationToken)).AsTask();
        }

        public Task<IAcquiredPreload> PreloadAsync(ViewResource resource, CancellationToken cancellationToken = default)
        {
            RequireAlive(resource);
            cancellationToken.ThrowIfCancellationRequested();
            var entry = Find(resource);
            if (entry != null)
            {
                RequireCurrent(entry.Version);
                ++entry.References;
                return Task.FromResult<IAcquiredPreload>(new AcquiredPreload(resource, () => ReleaseAsync(entry), thread));
            }

            RequireCapacity();
            return loading.RunAsync(token => LoadPreloadAsync(resource, token, cancellationToken)).AsTask();
        }

        private async ValueTask<IAcquiredPreload> LoadPreloadAsync(ViewResource resource, CancellationToken owner, CancellationToken caller)
        {
            var entry = await LoadAsync(resource, owner, caller);
            try
            {
                owner.ThrowIfCancellationRequested();
                caller.ThrowIfCancellationRequested();
                RequireAlive(resource);
                RequireCurrent(entry.Version);
                return new AcquiredPreload(resource, () => ReleaseAsync(entry), thread);
            }
            catch (Exception failure)
            {
                try
                {
                    await ReleaseAsync(entry);
                }
                catch (Exception cleanup)
                {
                    throw new ResourceLoadException(failure, Task.FromException(cleanup));
                }

                throw;
            }
        }

        private async ValueTask<IAcquiredView> LoadViewAsync(ViewResource resource, CancellationToken owner, CancellationToken caller)
        {
            var entry = await LoadAsync(resource, owner, caller);
            try
            {
                owner.ThrowIfCancellationRequested();
                caller.ThrowIfCancellationRequested();
                RequireAlive(resource);
                return CreateView(entry);
            }
            catch (Exception failure)
            {
                try
                {
                    await ReleaseFailedCreationAsync(entry, failure);
                }
                catch (Exception cleanup)
                {
                    throw new ResourceLoadException(failure, Task.FromException(cleanup));
                }

                throw;
            }
        }

        private async ValueTask<Entry> LoadAsync(ViewResource resource, CancellationToken owner, CancellationToken caller)
        {
            var version = contentVersion;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(owner, caller))
            {
                IAcquiredResource<GameObject> asset = null;
                try
                {
                    var key = resolveKey(resource);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        throw new InvalidOperationException("Resource key resolver returned no key.");
                    }

                    linked.Token.ThrowIfCancellationRequested();
                    asset = await loader.LoadAsync<GameObject>(key, linked.Token);
                    RequireThread();
                    linked.Token.ThrowIfCancellationRequested();
                    RequireAlive(resource);
                    RequireCurrent(version);
                    if (asset == null || asset.Asset == null)
                    {
                        throw new InvalidOperationException("Loader returned no live prefab.");
                    }

                    if (asset.Asset.GetComponent<View>() == null)
                    {
                        throw new InvalidOperationException("Loaded prefab requires a root View.");
                    }

                    RequireCurrent(version);

                    var entry = new Entry
                    {
                        Resource = resource,
                        Asset = asset,
                        References = 1,
                        Version = version
                    };
                    resident.Add(entry);
                    asset = null;
                    return entry;
                }
                catch (Exception failure)
                {
                    if (failure is ResourceLoadException deferredFailure)
                    {
                        try
                        {
                            await deferredFailure.CleanupCompletion;
                        }
                        catch (Exception cleanup)
                        {
                            RecordCleanupFailure(cleanup);
                        }
                    }

                    if (asset != null)
                    {
                        try
                        {
                            await asset.DisposeAsync();
                        }
                        catch (Exception cleanup)
                        {
                            RecordCleanupFailure(cleanup);
                            throw new ResourceLoadException(failure, Task.FromException(cleanup));
                        }
                    }

                    throw;
                }
            }
        }

        private IAcquiredView CreateView(Entry entry)
        {
            var instance = PrefabViewFactory.Create(entry.Asset.Asset, parent, staging.transform,
                () => RequireCurrent(entry.Version), configureView);
            var native = ((View)instance.View).gameObject;
            return new AcquiredView(instance.View, async _ =>
            {
                Exception failure = null;
                try
                {
                    await instance.DisposeAsync();
                }
                catch (Exception error)
                {
                    failure = error;
                    RecordCleanupFailure(error);
                }

                // 等待完整原生实例，而不是可能提前销毁的 View 组件；失败时不提前归还依赖。
                await PrefabViewFactory.WaitForReleasedInstanceAsync(native, failure);

                try
                {
                    await ReleaseAsync(entry);
                }
                catch (Exception cleanup)
                {
                    if (failure != null)
                    {
                        throw new AggregateException(failure, cleanup);
                    }

                    throw;
                }

                if (failure != null)
                {
                    throw failure;
                }
            }, thread);
        }

        private async ValueTask ReleaseFailedCreationAsync(Entry entry, Exception failure)
        {
            try
            {
                await PrefabViewFactory.WaitForFailedInstanceAsync(failure);
            }
            catch (Exception cleanup)
            {
                RecordCleanupFailure(cleanup);
                throw;
            }

            await ReleaseAsync(entry);
        }

        private Entry Find(ViewResource resource)
        {
            foreach (var entry in resident)
            {
                if (ReferenceEquals(entry.Version, contentVersion) && entry.References > 0 &&
                    entry.Resource.Equals(resource) && entry.Asset.Asset != null)
                {
                    return entry;
                }
            }

            return null;
        }

        private async ValueTask ReleaseAsync(Entry entry)
        {
            RequireThread();
            if (--entry.References != 0)
            {
                return;
            }

            resident.Remove(entry);
            try
            {
                await entry.Asset.DisposeAsync();
            }
            catch (Exception error)
            {
                RecordCleanupFailure(error);
                throw;
            }
        }

        private void RequireCapacity()
        {
            if ((long)loading.PendingOperationCount + rollbacks.PendingOperationCount >= loadCapacity)
            {
                throw new InvalidOperationException("Prefab load capacity exhausted.");
            }
        }

        private void RequireCurrent(object version)
        {
            RequireThread();
            if (disposal != null || !ReferenceEquals(version, contentVersion))
            {
                throw new OperationCanceledException("Prefab content changed during preparation.");
            }
        }

        private void RequireAlive(ViewResource resource)
        {
            RequireThread();
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (disposal != null || parent == null || staging == null)
            {
                throw new ObjectDisposedException(nameof(LoadedPrefabViewProvider));
            }
        }

        private void RequireThread()
        {
            UnityMainThread.Require();
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Prefab provider requires its owning Unity thread.");
            }
        }

        public ValueTask DisposeAsync()
        {
            RequireThread();
            if (disposal != null)
            {
                return new ValueTask(disposal.Task);
            }

            contentVersion = new object();
            disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = DisposeCoreAsync();
            return new ValueTask(disposal.Task);
        }

        private sealed class Entry
        {
            public ViewResource Resource;
            public IAcquiredResource<GameObject> Asset;
            public int References;
            public object Version;
        }
    }
}
