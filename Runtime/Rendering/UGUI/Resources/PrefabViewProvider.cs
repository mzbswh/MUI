using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>常驻 Prefab 目录；远程及 Addressables 加载使用独立提供方适配器。</summary>
    public sealed class PrefabViewProvider : IPreloadViewProvider, IDisposable, IAsyncDisposable, ICleanupResponsibilitySource
    {
        private readonly Dictionary<ViewResource, GameObject> prefabs = new Dictionary<ViewResource, GameObject>();
        private readonly Transform parent;
        private Action<View> configureView;
        private readonly GameObject staging;
        private bool disposed;

        /// <summary>
        /// configureView 在首次创建与缓存命中后的激活前同步调用，可注入当前资源加载器。
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
                CleanupResponsibility = PrefabViewFactory.CreateStagingCleanup(staging, "PrefabViewProvider.Staging", () =>
                {
                    disposed = true;
                    this.prefabs.Clear();
                    this.configureView = null;
                });
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

        /// <summary>提供方最终责任；未确认回滚占用挂载根时保留根节点，不销毁仍被使用的实例。</summary>
        public CleanupResponsibility CleanupResponsibility
        {
            get;
        }

        private GameObject GetPrefab(ViewResource resource)
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

            return prefab;
        }

        public Task<IAcquiredView> AcquireAsync(ViewResource resource, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IAcquiredView>(PrefabViewFactory.Create(GetPrefab(resource), parent, staging.transform,
                RequireAlive, configureView));
        }

        /// <summary>本地 Prefab 已驻留，返回独立持有凭证；无需启动实例创建。</summary>
        public Task<IAcquiredPreload> PreloadAsync(ViewResource resource, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var retained = GetPrefab(resource);
            return Task.FromResult<IAcquiredPreload>(new AcquiredPreload(resource, () =>
            {
                GC.KeepAlive(retained);
                retained = null;
                return default;
            }, Thread.CurrentThread.ManagedThreadId));
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
            var cleanup = DisposeAsync();
            if (cleanup.IsCompleted)
            {
                cleanup.GetAwaiter().GetResult();
                return;
            }
            // 保留原生同步兜底的启动语义；需要确认实际销毁时使用 DisposeAsync。
            _ = ObserveCleanupAsync(cleanup.AsTask());
        }

        public ValueTask DisposeAsync()
        {
            UnityMainThread.Require();
            return CleanupResponsibility.DisposeAsync();
        }

        private static async Task ObserveCleanupAsync(Task cleanup)
        {
            try
            {
                await cleanup;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
