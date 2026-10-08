using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.BasicExample
{
    /// <summary>最小页面接入：注册生成绑定、配置资源、打开页面并观察业务结果。</summary>
    public sealed class BasicDemo : MonoBehaviour
    {
        [SerializeField] private UIHost host = null;
        [SerializeField] private GameObject pagePrefab = null;
        [SerializeField] private UnityEngine.UI.Button reopenButton = null;
        [SerializeField] private UnityEngine.UI.Text reopenLabel = null;
        private readonly LifetimeScope resultLifetime = new LifetimeScope();
        private Route<BasicPageViewModel, Unit, int> route;
        private Task shutdown;
        private bool initialized;
        private bool pageRunning;
        private bool stopping;
        private bool shutdownObserved;

        private void Start() => _ = InitializeAsync();

        private async Task InitializeAsync()
        {
            try
            {
                if (host == null || pagePrefab == null || reopenButton == null || reopenLabel == null)
                {
                    throw new InvalidOperationException("Basic Demo needs a UIHost, BasicView prefab, reopen button, and label.");
                }

                reopenButton.gameObject.SetActive(false);
                Generated.BasicBindings.Initialize();
                route = BasicPageViewModelRoute.Create(() => new BasicPageViewModel());
                var provider = new PrefabViewProvider(host.transform,
                    new[] { new KeyValuePair<ViewResource, GameObject>(route.Resource, pagePrefab) }, ConfigurePage);
                try
                {
                    host.Initialize(provider, ownsProvider: true);
                }
                catch (Exception failure)
                {
                    try
                    {
                        provider.Dispose();
                    }
                    catch (Exception cleanupFailure)
                    {
                        throw new AggregateException("Basic Demo initialization and provider cleanup failed.", failure, cleanupFailure);
                    }

                    throw;
                }

                initialized = true;
                reopenButton.onClick.AddListener(OpenPage);
                OpenPage();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                await ObserveShutdownAsync();
            }
        }

        [ContextMenu("Open page")]
        public void OpenPage()
        {
            if (!initialized || stopping || pageRunning || host == null)
            {
                return;
            }

            // 同一轮页面会话包含打开、业务结果和视觉退出，重复点击不会启动第二个请求。
            pageRunning = true;
            reopenButton.gameObject.SetActive(false);
            _ = RunPageAsync();
        }

        /// <summary>原生 Cancel 由选中控件收到，实例在激活前接入当前宿主。</summary>
        private void ConfigurePage(View view)
        {
            foreach (var input in view.GetComponentsInChildren<UIBackInput>(true))
            {
                input.Host = host;
            }
        }

        private async Task RunPageAsync()
        {
            var offerReopen = false;
            var label = "Reopen";
            try
            {
                var outcome = await host.Navigator.OpenAsync(route, Unit.Value, resultLifetime.Token);
                if (!outcome.IsSuccess)
                {
                    Debug.LogWarning($"Open did not complete: {outcome.Status} ({outcome.Rejection}).");
                    offerReopen = true;
                    return;
                }

                var result = await outcome.Handle.WaitForResultAsync(resultLifetime.Token);
                Debug.Log($"Page result: {result.Status}, value={result.Value}");
                label = result.IsCompleted ? $"{result.Value}: Reopen" : "Reopen";
                // 业务结果在关闭提交时发布。场景中的重开按钮等画面退出后才恢复输入。
                var exited = await outcome.Handle.WaitForExitAsync(resultLifetime.Token);
                offerReopen = exited.IsCompleted;
            }
            catch (OperationCanceledException) when (resultLifetime.IsEnded)
            {
                // 场景退出只结束本示例的等待，宿主持有并完成页面清理。
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                pageRunning = false;
                if (offerReopen && initialized && !stopping && host != null &&
                    !host.Navigator.IsShutdown && reopenButton != null && reopenLabel != null)
                {
                    reopenLabel.text = label;
                    reopenButton.gameObject.SetActive(true);
                    var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                    if (eventSystem != null && !eventSystem.alreadySelecting)
                    {
                        eventSystem.SetSelectedGameObject(reopenButton.gameObject);
                    }
                }
            }
        }

        /// <summary>场景切换前显式等待此入口；重复调用观察同一次退出。</summary>
        public Task ShutdownAsync()
        {
            if (shutdown != null)
            {
                return shutdown;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            shutdown = completion.Task;
            _ = ShutdownCoreAsync(completion);
            return shutdown;
        }

        private async Task ShutdownCoreAsync(TaskCompletionSource<bool> completion)
        {
            var wasInitialized = initialized;
            initialized = false;
            stopping = true;
            var errors = new List<Exception>();
            try
            {
                if (reopenButton != null)
                {
                    reopenButton.onClick.RemoveListener(OpenPage);
                }
                resultLifetime.Cancel();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                if (wasInitialized && host != null)
                {
                    await host.ShutdownAsync();
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                await resultLifetime.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            if (errors.Count == 0)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(new AggregateException("Basic Demo cleanup failed.", errors));
            }
        }

        private void OnDestroy() => _ = ObserveShutdownAsync();

        private async Task ObserveShutdownAsync()
        {
            if (shutdownObserved)
            {
                return;
            }

            shutdownObserved = true;
            try
            {
                await ShutdownAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
