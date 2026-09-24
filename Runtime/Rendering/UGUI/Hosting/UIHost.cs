using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    [DisallowMultipleComponent]
    public sealed partial class UIHost : MonoBehaviour
    {
        [SerializeField]
        private RectTransform viewRoot = null;
        [SerializeField]
        private List<PrefabEntry> prefabs = new List<PrefabEntry>();
        [SerializeField]
        private bool automaticFramePump = true;
        private Navigator navigator;
        private IDisposable ownedProvider;
        private IAsyncDisposable asyncOwnedProvider;
        private Task shutdown;
        private bool logging;
        private bool initializing;
        private bool drivingFrame;
        private bool destroyed;

        public Navigator Navigator => navigator ?? throw new InvalidOperationException("UIHost has not been initialized.");

        /// <summary>自动模式每帧使用 Unity 非缩放时间；关闭后由项目调用 AdvanceFrame。</summary>
        public bool AutomaticFramePump
        {
            get => automaticFramePump;
            set => automaticFramePump = value;
        }

        /// <summary>
        /// 显式启动允许先安装生成的注册信息和项目服务。
        /// configureDefaultView 仅用于 Inspector 目录创建的 View，在新实例激活前同步配置；外部提供方自行配置。
        /// </summary>
        public void Initialize(IViewProvider provider = null,
            bool ownsProvider = false,
            ICloseConfirmationService closeConfirmationService = null,
            UIUserPreferences userPreferences = null,
            int cacheCapacity = 16,
            long? maxCachedEstimatedBytes = null,
            int cleanupCapacity = 16,
            Action<View> configureDefaultView = null)
        {
            BeginInitialization();
            try
            {
                if (cacheCapacity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
                }

                if (cleanupCapacity < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(cleanupCapacity));
                }

                if (maxCachedEstimatedBytes.HasValue && maxCachedEstimatedBytes.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxCachedEstimatedBytes));
                }

                if (provider != null && configureDefaultView != null)
                {
                    throw new ArgumentException("外部提供方请通过自己的实例配置入口接入，不能同时设置默认目录回调。", nameof(configureDefaultView));
                }

                var createdProvider = provider == null;
                if (createdProvider)
                {
                    provider = CreateDefaultProvider(configureDefaultView);
                    ownsProvider = true;
                }

                IAsyncDisposable asynchronousDisposable = null;
                IDisposable disposable = null;
                if (ownsProvider)
                {
                    asynchronousDisposable = provider as IAsyncDisposable;
                    if (asynchronousDisposable == null)
                    {
                        disposable = provider as IDisposable;
                    }

                    if (asynchronousDisposable == null && disposable == null)
                    {
                        throw new ArgumentException("An owned provider must support disposal.", nameof(provider));
                    }
                }

                try
                {
                    RequireInitializationCurrent();
                    navigator = new Navigator(provider, closeConfirmationService: closeConfirmationService,
                        userPreferences: userPreferences, cacheCapacity: cacheCapacity,
                        maxCachedEstimatedBytes: maxCachedEstimatedBytes,
                        cleanupCapacity: cleanupCapacity);
                }
                catch (Exception failure)
                {
                    if (createdProvider)
                    {
                        RollbackDefaultProvider((IDisposable)provider, failure);
                    }

                    throw;
                }

                // 导航器构造成功才接管提供方；失败时外部提供方仍完全属于调用者。
                asyncOwnedProvider = asynchronousDisposable;
                ownedProvider = disposable;
                UIErrors.Reported += LogError;
                logging = true;
            }
            finally
            {
                initializing = false;
            }
        }

        /// <summary>默认目录会修改原生层级，初始化期间必须拒绝回调再次进入任一初始化入口。</summary>
        private void BeginInitialization()
        {
            RequireInitializationCurrent();
            if (initializing)
            {
                throw new InvalidOperationException("UIHost 正在初始化，不能从回调中重复初始化。");
            }

            initializing = true;
        }

        private void RequireInitializationCurrent()
        {
            if (destroyed || this == null)
            {
                throw new ObjectDisposedException(nameof(UIHost));
            }

            if (navigator != null || shutdown != null || synchronousShutdownCompleted)
            {
                throw new InvalidOperationException("UIHost is already initialized or shut down.");
            }
        }

        private static void RollbackDefaultProvider(IDisposable provider, Exception failure)
        {
            try
            {
                provider.Dispose();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("UIHost 初始化失败，默认提供方回滚也失败。", failure, cleanupFailure);
            }
        }

        private PrefabViewProvider CreateDefaultProvider(Action<View> configureView = null)
        {
            if (viewRoot == null)
            {
                viewRoot = transform as RectTransform;
            }

            if (viewRoot == null)
            {
                throw new InvalidOperationException("UIHost requires a RectTransform root or an explicit provider.");
            }

            var catalog = new List<KeyValuePair<ViewResource, GameObject>>();
            foreach (var entry in prefabs)
            {
                if (entry == null || entry.Prefab == null)
                {
                    throw new InvalidOperationException("UIHost catalog contains an invalid Prefab.");
                }

                catalog.Add(new KeyValuePair<ViewResource, GameObject>(new ViewResource(entry.Key, entry.Version), entry.Prefab));
            }

            return new PrefabViewProvider(viewRoot, catalog, configureView);
        }

        private static void LogError(Exception error) => Debug.LogException(error);

        private void Update()
        {
            if (automaticFramePump)
            {
                DriveFrame(Time.unscaledDeltaTime);
            }
        }

        /// <summary>启用的宿主可手动驱动请求派发和界面 Tick；自动模式下拒绝调用。</summary>
        public void AdvanceFrame(float unscaledDeltaTime)
        {
            if (this == null || destroyed)
            {
                throw new ObjectDisposedException(nameof(UIHost));
            }

            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("停用的 UIHost 不能推进界面帧。");
            }

            if (automaticFramePump)
            {
                throw new InvalidOperationException("请先关闭 UIHost 的自动帧驱动。");
            }

            if (navigator == null)
            {
                throw new InvalidOperationException("UIHost 尚未初始化。");
            }

            DriveFrame(unscaledDeltaTime);
        }

        private void DriveFrame(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime < 0 || float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            if (drivingFrame)
            {
                throw new InvalidOperationException("UIHost 不能在当前帧的回调中再次推进帧。");
            }

            if (navigator == null || navigator.IsShutdown)
            {
                return;
            }

            drivingFrame = true;
            try
            {
                navigator.Pump();
                navigator.Tick(unscaledDeltaTime);
            }
            finally
            {
                drivingFrame = false;
            }
        }

        public ValueTask ShutdownAsync()
        {
            if (navigator != null && navigator.Mode == LifetimeMode.Synchronous)
            {
                Shutdown();
                return default;
            }

            if (navigator != null && !navigator.CanAwaitShutdown)
            {
                return new ValueTask(Task.FromException(new InvalidOperationException("A lifecycle hook or command cannot await its own UIHost shutdown.")));
            }

            return new ValueTask(BeginShutdown());
        }

        private Task BeginShutdown()
        {
            BackInputCompleted = null;
            if (shutdown != null)
            {
                return shutdown;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            shutdown = completion.Task;
            _ = ShutdownCoreAsync(completion);
            return completion.Task;
        }

        private async Task ShutdownCoreAsync(TaskCompletionSource<bool> completion)
        {
            var errors = new List<Exception>();
            try
            {
                if (navigator != null)
                {
                    await navigator.RequestShutdownForHost();
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                if (asyncOwnedProvider != null)
                {
                    await asyncOwnedProvider.DisposeAsync();
                }
                else if (ownedProvider != null)
                {
                    ownedProvider.Dispose();
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            finally
            {
                asyncOwnedProvider = null;
                ownedProvider = null;
                if (logging)
                {
                    UIErrors.Reported -= LogError;
                    logging = false;
                }
            }

            if (errors.Count == 0)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(new AggregateException("UIHost shutdown failed.", errors));
            }
        }

        private void OnDestroy()
        {
            destroyed = true;
            BackInputCompleted = null;
            if (navigator == null)
            {
                // 初始化尚未交付时没有接管提供方；外层创建事务负责回滚，不启动异步清理。
                return;
            }
            if (navigator != null && navigator.Mode == LifetimeMode.Synchronous)
            {
                try
                {
                    Shutdown();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
                return;
            }

            _ = ObserveShutdownAsync(BeginShutdown());
        }

        private static async Task ObserveShutdownAsync(Task completion)
        {
            try
            {
                await completion;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        [Serializable]
        private sealed class PrefabEntry
        {
            public string Key = string.Empty;
            public string Version = "1";
            public GameObject Prefab = null;
        }
    }
}
