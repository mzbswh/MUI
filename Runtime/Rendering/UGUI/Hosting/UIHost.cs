using System;
using System.Collections.Generic;
using System.Threading;
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
        private MUISettings settings = null;
        [SerializeField]
        private RectTransform viewRoot = null;
        [SerializeField]
        private List<PrefabEntry> prefabs = new List<PrefabEntry>();
        private Navigator navigator;
        private UIThreadDispatcher dispatcher;
        private HostProviderCleanup ownedProviderCleanup;
        private Task shutdown;
        private bool logging;
        private bool initializing;
        private bool drivingFrame;
        private bool destroyed;

        public Navigator Navigator => navigator ?? throw new InvalidOperationException("UIHost has not been initialized.");

        public bool IsInitialized => navigator != null;

        public bool IsShutdown => navigator != null && navigator.IsShutdown;

        public MUISettings Settings
        {
            get => settings;
            set
            {
                UnityMainThread.Require();
                RequireInitializationCurrent();
                if (initializing)
                {
                    throw new InvalidOperationException("初始化期间不能更换宿主配置。");
                }
                settings = value;
            }
        }

        /// <summary>在初始化时捕获的主线程入口，可交给项目后台服务提交表现状态。</summary>
        public UIThreadDispatcher Dispatcher => dispatcher ?? throw new InvalidOperationException("UIHost has not been initialized.");

        /// <summary>自动模式每帧使用 Unity 非缩放时间；关闭后由项目调用 AdvanceFrame。</summary>
        public bool AutomaticFramePump => settings == null || settings.AutomaticFramePump;

        public RoutePolicy ResolvePolicy(string preset = null, string layer = null, RoutePolicyOverrides overrides = null)
        {
            UnityMainThread.Require();
            return settings == null ? MUISettings.ResolveBuiltInPolicy(preset, layer, overrides)
                : settings.ResolvePolicy(preset, layer, overrides);
        }

        /// <summary>只读定位当前页面原生对象；不会创建、激活或调用业务。</summary>
        public bool TryGetView(ViewHandle handle, out View view)
        {
            UnityMainThread.Require();
            view = null;
            if (navigator == null || !navigator.TryGetView(handle, out var candidate))
            {
                return false;
            }
            view = candidate as View;
            return view != null && view.IsAlive;
        }

        /// <summary>
        /// 显式启动允许先安装生成的注册信息和项目服务。
        /// configureDefaultView 仅用于 Inspector 目录创建的 View，在新实例激活前同步配置；外部提供方自行配置。
        /// </summary>
        public void Initialize(IViewProvider provider = null,
            bool ownsProvider = false,
            ICloseConfirmationService closeConfirmationService = null,
            UIUserPreferences userPreferences = null,
            int? cacheCapacity = null,
            long? maxCachedEstimatedBytes = null,
            int? cleanupCapacity = null,
            Action<View> configureDefaultView = null,
            int? terminalCapacity = null,
            TimeSpan? terminalDuration = null,
            int? queueCapacity = null,
            int? preloadCapacity = null,
            RenderOrderOptions renderOrder = null)
        {
            BeginInitialization();
            try
            {
                if (settings != null)
                {
                    settings.RequireValid();
                }
                // 在原生提供方可能触发项目回调前复制配置，避免初始化使用两次编辑之间的值。
                var resolvedCacheCapacity = cacheCapacity ?? (settings == null ? 16 : settings.CacheCapacity);
                var resolvedCleanupCapacity = cleanupCapacity ?? (settings == null ? 16 : settings.CleanupCapacity);
                var resolvedTerminalCapacity = terminalCapacity ?? (settings == null ? 256 : settings.TerminalCapacity);
                var resolvedQueueCapacity = queueCapacity ?? (settings == null ? 64 : settings.QueueCapacity);
                var resolvedRenderOrder = renderOrder ?? (settings == null ? new RenderOrderOptions() : settings.CreateRenderOrderOptions());
                var resolvedPreloadCapacity = preloadCapacity ?? (settings == null ? 32 : settings.PreloadCapacity);
                if (cacheCapacity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
                }

                if (cleanupCapacity < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(cleanupCapacity));
                }

                if (queueCapacity < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(queueCapacity));
                }

                if (preloadCapacity < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(preloadCapacity));
                }

                if (terminalCapacity < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(terminalCapacity));
                }

                if (terminalDuration.HasValue && terminalDuration.Value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(terminalDuration));
                }

                if (maxCachedEstimatedBytes.HasValue && maxCachedEstimatedBytes.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxCachedEstimatedBytes));
                }

                if (provider != null && configureDefaultView != null)
                {
                    throw new ArgumentException("外部提供方请通过自己的实例配置入口接入，不能同时设置默认目录回调。", nameof(configureDefaultView));
                }

                var capturedDispatcher = UIThreadDispatcher.CaptureCurrent();
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
                        userPreferences: userPreferences, cacheCapacity: resolvedCacheCapacity,
                        maxCachedEstimatedBytes: maxCachedEstimatedBytes,
                        cleanupCapacity: resolvedCleanupCapacity,
                        terminalCapacity: resolvedTerminalCapacity,
                        terminalDuration: terminalDuration, queueCapacity: resolvedQueueCapacity,
                        preloadCapacity: resolvedPreloadCapacity, renderOrder: resolvedRenderOrder);
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
                if (ownsProvider)
                {
                    ownedProviderCleanup = new HostProviderCleanup(navigator, asynchronousDisposable, disposable);
                }
                dispatcher = capturedDispatcher;
                UnityErrorLogging.RegisterHost();
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
            UnityMainThread.Require();
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

            if (navigator != null || shutdown != null)
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

        private void Update()
        {
            if (AutomaticFramePump)
            {
                DriveFrame(Time.unscaledDeltaTime);
            }
        }

        /// <summary>启用的宿主可手动驱动请求派发和界面 Tick；自动模式下拒绝调用。</summary>
        public void AdvanceFrame(float unscaledDeltaTime)
        {
            UnityMainThread.Require();
            if (this == null || destroyed)
            {
                throw new ObjectDisposedException(nameof(UIHost));
            }

            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("停用的 UIHost 不能推进界面帧。");
            }

            if (AutomaticFramePump)
            {
                throw new InvalidOperationException("请先在 MUI 配置中关闭“自动更新界面”。");
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

        /// <summary>
        /// 停止宿主并等待实际清理。取消只结束当前等待，退出仍继续；后续调用观察同一次清理。
        /// 即使传入已取消的令牌，也会先接纳退出，禁止新导航请求。
        /// </summary>
        public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            UnityMainThread.Require();
            if (navigator == null)
            {
                throw new InvalidOperationException("UIHost shutdown requires initialization.");
            }

            if (!navigator.CanAwaitShutdown || (ownedProviderCleanup != null &&
                ownedProviderCleanup.Responsibility.IsReleaseCallbackActive))
            {
                return new ValueTask(Task.FromException(new InvalidOperationException("A lifecycle hook, command or provider cleanup cannot await its own UIHost shutdown.")));
            }

            var completion = BeginShutdown();
            return new ValueTask(cancellationToken.CanBeCanceled
                ? AsyncWait.WithCancellation(completion, cancellationToken)
                : completion);
        }

        private Task BeginShutdown()
        {
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
            Task navigatorShutdown;
            try
            {
                navigatorShutdown = navigator == null ? Task.CompletedTask : navigator.RequestShutdownForHost();
            }
            catch (Exception error)
            {
                if (navigator == null || !navigator.IsShutdown)
                {
                    shutdown = null;
                }

                completion.TrySetException(error);
                return;
            }

            if (navigator == null || navigator.IsShutdown)
            {
                BackInputCompleted = null;
            }
            var errors = new List<Exception>();
            Task providerCleanup = null;
            try
            {
                // 在等待迟到准备前登记最终拥有者，原生宿主销毁后仍可查询这段在途清理。
                if (ownedProviderCleanup != null)
                {
                    providerCleanup = ownedProviderCleanup.DisposeAsync(navigatorShutdown).AsTask();
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            try
            {
                await navigatorShutdown;
            }
            catch (Exception error)
            {
                if (navigator == null || !navigator.IsShutdown)
                {
                    shutdown = null;
                    completion.TrySetException(error);
                    return;
                }

                errors.Add(error);
            }

            BackInputCompleted = null;
            try
            {
                if (providerCleanup != null)
                {
                    await providerCleanup;
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            finally
            {
                if (ownedProviderCleanup != null && ownedProviderCleanup.IsConfirmed)
                {
                    ownedProviderCleanup = null;
                }

                if (logging)
                {
                    UnityErrorLogging.UnregisterHost();
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
            UnregisterSharedInput();
            BackInputCompleted = null;
            if (navigator == null)
            {
                // 初始化尚未交付时没有接管提供方；外层创建事务负责回滚，不启动异步清理。
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
                UnityErrorLogging.Report(error);
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
