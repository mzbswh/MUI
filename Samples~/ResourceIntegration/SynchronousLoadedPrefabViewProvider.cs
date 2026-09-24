using System;
using System.Collections.Generic;
using System.Threading;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>
    /// 从同步资源后端按需创建界面；预加载与界面独立持有同一代际的 Prefab。
    /// 后端必须保证归还源资源不破坏存活克隆，因此无需等待 Unity 的延迟销毁。
    /// </summary>
    public sealed class SynchronousLoadedPrefabViewProvider : ISynchronousPreloadViewProvider,
        IViewContentVersion, IDisposable
    {
        private ISynchronousInstantiableResourceLoader loader;
        private Func<ViewResource, string> resolveKey;
        private readonly Transform parent;
        private Action<View> configureView;
        private readonly GameObject staging;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly int capacity;
        private readonly List<Entry> entries = new List<Entry>();
        private object version = new object();
        private bool disposed;
        private bool operating;
        private int reservations;
        private Exception firstCleanupFailure;
        private int cleanupFailureCount;

        /// <summary>
        /// configureView 在每个新实例激活前同步配置 View；缓存复用不重复调用。
        /// 回调不能激活、销毁、重新挂载实例或启动异步工作，加载器仍由项目持有。
        /// </summary>
        public SynchronousLoadedPrefabViewProvider(Transform parent,
            ISynchronousInstantiableResourceLoader loader, Func<ViewResource, string> resolveKey,
            int residentCapacity = 32, Action<View> configureView = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
            if (residentCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(residentCapacity));
            }
            this.parent = parent;
            this.configureView = configureView;
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.resolveKey = resolveKey ?? throw new ArgumentNullException(nameof(resolveKey));
            capacity = residentCapacity;
            staging = new GameObject("MUI Synchronous Prefab Staging", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(parent, false);
        }

        /// <summary>累计失败清理次数；只保留首个异常，避免长期运行时错误记录无限增长。</summary>
        public int CleanupFailureCount
        {
            get
            {
                RequireThread();
                return cleanupFailureCount;
            }
        }

        /// <summary>首个已知清理错误；提供方关闭后仍可读取，包括外部凭证迟到释放产生的错误。</summary>
        public Exception FirstCleanupFailure
        {
            get
            {
                RequireThread();
                return firstCleanupFailure;
            }
        }

        /// <summary>版本仅在显式失效或提供方关闭时变化，读取不触发加载。</summary>
        public object ContentVersion
        {
            get
            {
                RequireThread();
                return version;
            }
        }

        /// <summary>包含旧代际驻留项和释放失败项；失败不能提前归还额度。</summary>
        public int ReservationCount
        {
            get
            {
                RequireThread();
                return reservations;
            }
        }

        public SyncCreateAvailability GetSyncAvailability(ViewResource resource)
        {
            RequireAlive(resource);
            // 表达后端具有直接创建能力，不保证路径存在；实际加载错误由 Create 报告。
            return SyncCreateAvailability.Available;
        }

        /// <summary>旧凭证仍有效，但新请求不能复用旧代际内容。</summary>
        public void Invalidate()
        {
            RequireThread();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SynchronousLoadedPrefabViewProvider));
            }
            version = new object();
        }

        public ISynchronousPreloadLease Preload(ViewResource resource)
        {
            BeginOperation(resource);
            try
            {
                var entry = Acquire(resource);
                return new SynchronousPreloadLease(resource, () => Release(entry));
            }
            finally
            {
                operating = false;
            }
        }

        public ISynchronousViewLease Create(ViewResource resource)
        {
            BeginOperation(resource);
            Entry entry = null;
            try
            {
                entry = Acquire(resource);
                var instance = PrefabViewFactory.Create(entry.Prefab, parent, staging.transform,
                    () => RequireCurrent(entry.Version), configureView);
                return new SynchronousViewLease(instance.View, _ => ReleaseView(instance, entry));
            }
            catch (Exception failure)
            {
                if (entry != null)
                {
                    if (failure is SynchronousResourceLoadException rollback)
                    {
                        RecordCleanupFailure(rollback.CleanupError);
                    }

                    try
                    {
                        // 后端契约允许立即归还，即使失败克隆仍等待引擎销毁。
                        Release(entry);
                    }
                    catch (Exception cleanup)
                    {
                        throw new SynchronousResourceLoadException(failure, cleanup);
                    }
                }
                throw;
            }
            finally
            {
                operating = false;
            }
        }

        private void ReleaseView(SynchronousViewLease instance, Entry entry)
        {
            RequireThread();
            Exception failure = null;
            try
            {
                instance.Dispose();
            }
            catch (Exception error)
            {
                failure = error;
                RecordCleanupFailure(error);
            }
            try
            {
                Release(entry);
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
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private Entry Acquire(ViewResource resource)
        {
            var captured = version;
            foreach (var entry in entries)
            {
                if (ReferenceEquals(entry.Version, captured) && entry.Resource.Equals(resource) && entry.Prefab != null)
                {
                    checked
                    {
                        ++entry.References;
                    }
                    return entry;
                }
            }
            if (reservations >= capacity)
            {
                throw new InvalidOperationException("Synchronous prefab resident capacity exhausted.");
            }

            ++reservations;
            ISynchronousResourceLease<GameObject> asset = null;
            try
            {
                var key = resolveKey(resource);
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new InvalidOperationException("Prefab key resolver returned no key.");
                }
                RequireCurrent(captured);
                asset = loader.Load<GameObject>(key);
                if (asset == null)
                {
                    throw new InvalidOperationException("Loader returned no synchronous prefab lease.");
                }
                var prefab = asset.Asset;
                RequireCurrent(captured);
                if (prefab == null || prefab.GetComponent<View>() == null)
                {
                    throw new InvalidOperationException("Loaded prefab requires a live root View.");
                }
                var entry = new Entry
                {
                    Resource = resource,
                    Version = captured,
                    Asset = asset,
                    Prefab = prefab,
                    References = 1
                };
                entries.Add(entry);
                return entry;
            }
            catch (Exception failure)
            {
                if (failure is SynchronousResourceLoadException rollback)
                {
                    RecordCleanupFailure(rollback.CleanupError);
                }
                else if (failure is ResourceLoadException)
                {
                    // 同步后端意外报告异步残留时，同样保留诊断，不启动或等待清理任务。
                    RecordCleanupFailure(failure);
                }

                try
                {
                    if (asset != null)
                    {
                        asset.Dispose();
                    }
                    if (!(failure is SynchronousResourceLoadException) && !(failure is ResourceLoadException))
                    {
                        --reservations;
                    }
                }
                catch (Exception cleanup)
                {
                    RecordCleanupFailure(cleanup);
                    throw new SynchronousResourceLoadException(failure, cleanup);
                }
                throw;
            }
        }

        private void Release(Entry entry)
        {
            RequireThread();
            if (--entry.References != 0)
            {
                return;
            }
            entries.Remove(entry);
            var asset = entry.Asset;
            entry.Asset = null;
            entry.Prefab = null;
            // 凭证已移出目录，释放失败也不会被重复使用；失败额度继续占用。
            try
            {
                asset.Dispose();
            }
            catch (Exception error)
            {
                RecordCleanupFailure(error);
                throw;
            }

            --reservations;
        }

        private void BeginOperation(ViewResource resource)
        {
            RequireAlive(resource);
            if (operating)
            {
                throw new InvalidOperationException("Synchronous prefab loading cannot reenter its provider.");
            }
            operating = true;
        }

        private void RequireCurrent(object captured)
        {
            RequireThread();
            if (disposed || parent == null || staging == null || !ReferenceEquals(captured, version))
            {
                throw new OperationCanceledException("Synchronous prefab provider changed during creation.");
            }
        }

        private void RequireAlive(ViewResource resource)
        {
            RequireThread();
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (disposed || parent == null || staging == null)
            {
                throw new ObjectDisposedException(nameof(SynchronousLoadedPrefabViewProvider));
            }
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Synchronous prefab provider requires its owning Unity thread.");
            }
        }

        public void Dispose()
        {
            RequireThread();
            if (operating)
            {
                throw new InvalidOperationException("Cannot dispose a prefab provider during creation.");
            }
            if (!disposed)
            {
                disposed = true;
                version = new object();
                // 配置仅供创建使用；借用的加载器不由提供方销毁。
                loader = null;
                resolveKey = null;
                configureView = null;
                if (staging != null)
                {
                    try
                    {
                        UnityEngine.Object.Destroy(staging);
                    }
                    catch (Exception error)
                    {
                        RecordCleanupFailure(error);
                    }
                }
            }

            if (firstCleanupFailure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstCleanupFailure).Throw();
            }
            // 外部凭证继续持有资源并可正常释放；提供方不夺取调用者的所有权。
        }

        private void RecordCleanupFailure(Exception error)
        {
            if (firstCleanupFailure == null)
            {
                firstCleanupFailure = error;
            }

            if (cleanupFailureCount < int.MaxValue)
            {
                ++cleanupFailureCount;
            }
        }

        private sealed class Entry
        {
            internal ViewResource Resource;
            internal object Version;
            internal ISynchronousResourceLease<GameObject> Asset;
            internal GameObject Prefab;
            internal int References;
        }
    }
}
