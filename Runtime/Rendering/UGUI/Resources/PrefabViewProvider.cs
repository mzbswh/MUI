using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>常驻 Prefab 目录；远程及 Addressables 加载使用独立提供方适配器。</summary>
    public sealed class PrefabViewProvider : IViewProvider, ISynchronousPreloadViewProvider, IDisposable
    {
        private readonly Dictionary<ViewResource, GameObject> prefabs = new Dictionary<ViewResource, GameObject>();
        private readonly Transform parent;
        private Action<View> configureView;
        private readonly GameObject staging;
        private bool disposed;

        /// <summary>
        /// configureView 在每个新实例激活前同步调用，可配置资源加载器；缓存复用不重复调用。
        /// 回调只能配置实例，不应激活、销毁、重新挂载实例或启动异步工作。
        /// </summary>
        public PrefabViewProvider(Transform parent, IEnumerable<KeyValuePair<ViewResource, GameObject>> prefabs,
            Action<View> configureView = null)
        {
            UnityMainThread.Require();
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (prefabs == null)
            {
                throw new ArgumentNullException(nameof(prefabs));
            }

            this.parent = parent;
            this.configureView = configureView;
            foreach (var entry in prefabs)
            {
                if (entry.Key == null || entry.Value == null)
                {
                    throw new ArgumentException("Catalog contains a null key or prefab.", nameof(prefabs));
                }

                this.prefabs.Add(entry.Key, entry.Value);
            }

            var createdStaging = new GameObject("MUI Staging", typeof(RectTransform));
            try
            {
                createdStaging.SetActive(false);
                if (parent == null)
                {
                    throw new InvalidOperationException("Prefab mounting root was destroyed while reading the catalog.");
                }

                createdStaging.transform.SetParent(parent, false);
                if (createdStaging == null || parent == null || createdStaging.transform.parent != parent)
                {
                    throw new InvalidOperationException("Prefab staging root changed during provider setup.");
                }

                staging = createdStaging;
            }
            catch (Exception failure)
            {
                try
                {
                    if (createdStaging != null)
                    {
                        UnityEngine.Object.Destroy(createdStaging);
                    }
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Prefab provider setup and staging cleanup failed.", failure, cleanupFailure);
                }

                throw;
            }
        }

        public SyncCreateAvailability GetSyncAvailability(ViewResource resource)
        {
            RequireAlive();
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            return prefabs.TryGetValue(resource, out var prefab) && prefab != null ? SyncCreateAvailability.Available : SyncCreateAvailability.Unsupported;
        }

        /// <summary>常驻目录无需额外加载；凭证独立保持 Prefab 引用，不创建或销毁界面实例。</summary>
        public ISynchronousPreloadLease Preload(ViewResource resource)
        {
            if (GetSyncAvailability(resource) != SyncCreateAvailability.Available)
            {
                throw new InvalidOperationException($"Prefab is not resident: {resource}.");
            }
            var retained = prefabs[resource];
            return new SynchronousPreloadLease(resource, () =>
            {
                // 仅归还本凭证的引用，原生资源卸载由资源系统决定。
                GC.KeepAlive(retained);
                retained = null;
            });
        }

        public IViewLease Create(ViewResource resource) => CreateCore(resource);

        ISynchronousViewLease ISynchronousViewProvider.Create(ViewResource resource) => CreateCore(resource);

        private SynchronousViewLease CreateCore(ViewResource resource)
        {
            RequireAlive();
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (!prefabs.TryGetValue(resource, out var prefab) || prefab == null)
            {
                throw new InvalidOperationException($"Prefab is not in the resident catalog: {resource}.");
            }

            return PrefabViewFactory.Create(prefab, parent, staging.transform,
                RequireAlive, configureView);
        }

        public ValueTask<IViewLease> CreateAsync(ViewResource resource, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IViewLease>(Create(resource));
        }

        private void RequireAlive()
        {
            UnityMainThread.Require();
            if (disposed || parent == null || staging == null)
            {
                throw new ObjectDisposedException(nameof(PrefabViewProvider));
            }
        }

        public void Dispose()
        {
            UnityMainThread.Require();
            if (disposed)
            {
                return;
            }

            disposed = true;
            prefabs.Clear();
            // 提供方可能仍被外部持有，结束后不再保留项目配置回调。
            configureView = null;
            if (staging != null)
            {
                UnityEngine.Object.Destroy(staging);
            }
        }
    }
}
