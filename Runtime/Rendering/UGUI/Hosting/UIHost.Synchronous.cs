using System;
using System.Collections.Generic;
using MUI.Navigation;
using MUI.Resources;

namespace MUI.UGUI
{
    public sealed partial class UIHost
    {
        private bool synchronousShuttingDown;
        private bool synchronousShutdownCompleted;
        private Exception synchronousShutdownError;

        /// <summary>
        /// 安装纯同步导航；默认使用 Inspector 中的 Prefab 目录，外部提供方只需同步契约。
        /// configureDefaultView 在目录中新实例激活前直接调用，不创建任务；外部提供方自行配置。
        /// </summary>
        public void InitializeSynchronous(ISynchronousViewProvider provider = null,
            bool ownsProvider = false, UIUserPreferences userPreferences = null,
            int cacheCapacity = 16, long? maxCachedEstimatedBytes = null,
            int preloadCapacity = 32,
            Action<View> configureDefaultView = null,
            int terminalCapacity = 256,
            TimeSpan? terminalDuration = null)
        {
            BeginInitialization();
            try
            {
                // 可直接判定的无效配置必须先拒绝，不能为此创建再销毁临时层级。
                if (cacheCapacity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
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

                var createdProvider = provider == null;
                if (createdProvider)
                {
                    provider = CreateDefaultProvider(configureDefaultView);
                    ownsProvider = true;
                }

                var disposable = ownsProvider ? provider as IDisposable : null;
                if (ownsProvider && disposable == null)
                {
                    throw new ArgumentException("An owned synchronous provider must implement IDisposable.", nameof(provider));
                }

                try
                {
                    RequireInitializationCurrent();
                    navigator = Navigator.CreateSynchronous(provider, userPreferences: userPreferences,
                        cacheCapacity: cacheCapacity, maxCachedEstimatedBytes: maxCachedEstimatedBytes,
                        preloadCapacity: preloadCapacity, terminalCapacity: terminalCapacity,
                        terminalDuration: terminalDuration);
                }
                catch (Exception failure)
                {
                    // 初始化失败时，外部提供方仍由调用者拥有；内部创建的目录立即归还。
                    if (createdProvider)
                    {
                        RollbackDefaultProvider(disposable, failure);
                    }
                    throw;
                }

                ownedProvider = disposable;
                UnityErrorLogging.RegisterHost();
                logging = true;
            }
            finally
            {
                initializing = false;
            }
        }

        /// <summary>直接停止导航、释放拥有的同步提供方并撤销平台监听。</summary>
        public void Shutdown()
        {
            if (navigator == null || navigator.Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("UIHost synchronous shutdown requires synchronous initialization.");
            }
            if (synchronousShutdownCompleted)
            {
                if (synchronousShutdownError != null)
                {
                    throw synchronousShutdownError;
                }
                return;
            }

            if (synchronousShuttingDown || !navigator.CanAwaitShutdown)
            {
                throw new InvalidOperationException("Cannot shut down UIHost from navigation callbacks.");
            }
            synchronousShuttingDown = true;
            var errors = new List<Exception>();
            try
            {
                navigator.Shutdown();
            }
            catch (Exception error)
            {
                // 预检拒绝时导航器仍在运行，不能提前销毁其提供方。
                if (!navigator.IsShutdown)
                {
                    synchronousShuttingDown = false;
                    throw;
                }
                errors.Add(error);
            }

            BackInputCompleted = null;
            try
            {
                if (ownedProvider != null)
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
                ownedProvider = null;
                if (logging)
                {
                    UnityErrorLogging.UnregisterHost();
                    logging = false;
                }
                synchronousShutdownCompleted = true;
                synchronousShuttingDown = false;
            }

            if (errors.Count != 0)
            {
                synchronousShutdownError = new AggregateException("Synchronous UIHost shutdown failed.", errors);
                throw synchronousShutdownError;
            }
        }

        /// <summary>直接清理本宿主预加载与停用页面，不操作项目资源池。</summary>
        public void ClearInactiveContent() => Navigator.ClearInactiveContent();
    }
}
