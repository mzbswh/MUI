using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using MUI.Samples.ResourceIntegration;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>本地资源导航示例：立即完成的业务逻辑通过统一异步协议执行导航和清理。</summary>
    public sealed partial class LocalNavigationDemo : MonoBehaviour
    {
        private const string FailingOpenTitle = "演示本地替换准备失败";
        private const string FailingTitle = "演示本地提交失败";
        [SerializeField] private UIHost host = null;
        [SerializeField] private GameObject pagePrefab = null;
        [SerializeField] private string resourcesPath = string.Empty;
        [SerializeField] private bool allowClose = true;
        private Route<NavigationPageViewModel, string, int> route;
        private Route<NavigationPageViewModel, string, int> batchRoute;
        private ViewHandle<int> current;
        private int createdPages;
        private bool initialized;
        private readonly LifetimeScope observationLifetime = new LifetimeScope();
        private readonly HashSet<ViewHandle> observedPages = new HashSet<ViewHandle>();

        private async void Start()
        {
            try
            {
                if (host == null || (pagePrefab == null && string.IsNullOrWhiteSpace(resourcesPath)))
                {
                    throw new InvalidOperationException("请配置 UIHost，并指定 Prefab 或 Resources 相对路径。");
                }

                var resource = new ViewResource("NavigationPage");
                IViewProvider provider;
                if (!string.IsNullOrWhiteSpace(resourcesPath))
                {
                    var path = resourcesPath;
                    var loader = new MUI.Samples.ResourceIntegration.UnityResourcesLoader();
                    provider = new LoadedPrefabViewProvider(host.transform, loader, _ => path,
                        configureView: view => view.ConfigureResources(loader));
                }
                else
                {
                    provider = new PrefabViewProvider(host.transform,
                        new[] { new KeyValuePair<ViewResource, GameObject>(resource, pagePrefab) });
                }
                try
                {
                    host.Initialize(provider, ownsProvider: true, cacheCapacity: 2);
                }
                catch
                {
                    if (provider is IAsyncDisposable asynchronous)
                    {
                        await asynchronous.DisposeAsync();
                    }
                    else if (provider is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    throw;
                }

                initialized = true;
                route = new Route<NavigationPageViewModel, string, int>("demo.local", resource,
                    () => new NavigationPageViewModel(), _ => new PagePresenter(this),
                    NavigationPageViewModelBindingFactory.Create,
                    policy: host.ResolvePolicy(overrides: new RoutePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }));
                batchRoute = new Route<NavigationPageViewModel, string, int>("demo.local.batch", resource,
                    () => new NavigationPageViewModel(), _ => new PagePresenter(this),
                    NavigationPageViewModelBindingFactory.Create,
                    policy: host.ResolvePolicy(layer: "Popup", overrides: new RoutePolicyOverrides
                    {
                        AllowMultiple = true,
                        MaxInstances = 3,
                        Overflow = OverflowPolicy.CloseOldest,
                        ExistingInstance = ExistingInstancePolicy.Reject
                    }));
                await OpenAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        [ContextMenu("本地打开或缓存重开")]
        public void Open() => Run(OpenAsync);

        private async Task OpenAsync()
        {
            if (!initialized || host == null)
            {
                return;
            }
            var result = await host.Navigator.OpenAsync(route, "本地资源的顶层界面");
            current = result.Handle;
            ObservePage(current);
            Debug.Log($"本地打开={result.Status}，实际创建次数={createdPages}，缓存数={host.Navigator.CachedViewCount}");
        }

        [ContextMenu("本地关闭")]
        public void Close() => Run(CloseAsync);

        private async Task CloseAsync()
        {
            if (initialized && host != null)
            {
                Debug.Log($"本地关闭={(await host.Navigator.CloseAsync(current)).Status}");
                ReadResult();
            }
        }

        [ContextMenu("本地返回")]
        public void Back() => Run(BackAsync);

        private async Task BackAsync()
        {
            if (initialized && host != null)
            {
                Debug.Log($"本地返回={(await host.Navigator.BackAsync()).Status}");
            }
        }

        [ContextMenu("直接读取结果")]
        public void ReadResult()
        {
            if (current.IsValid && host != null && current.TryGetResult(out var result))
            {
                Debug.Log($"业务结果={result.Status}，缓存数={host.Navigator.CachedViewCount}");
            }
        }

        /// <summary>每次激活只订阅一次；通知直接执行，不创建任务或依赖轮询。</summary>
        private void ObservePage(ViewHandle<int> handle)
        {
            if (!handle.IsValid || !observedPages.Add(handle.Identity))
            {
                return;
            }

            IDisposable readinessSubscription = null;
            try
            {
                // 已发布的就绪状态会立即通知；Ready 不等于页面当前允许输入。
                readinessSubscription = handle.ObserveReadiness(observationLifetime, readiness =>
                    Debug.Log($"本地页面={handle.Identity}，就绪={readiness.Status}"));
                handle.ObserveResult(observationLifetime, result =>
                {
                    observedPages.Remove(handle.Identity);
                    Debug.Log($"本地页面={handle.Identity}，结果={result.Status}，值={result.Value}，清理={result.Cleanup}");
                    // 通知中需要打开下一页时使用 Navigator.PostOpen，避免重入导航事务。
                });
            }
            catch
            {
                if (readinessSubscription != null)
                {
                    readinessSubscription.Dispose();
                }
                observedPages.Remove(handle.Identity);
                throw;
            }
        }

        [ContextMenu("本地更新参数")]
        public void UpdateArgs() => UpdateTitle("本地更新后的标题");

        [ContextMenu("本地参数提交失败并故障关闭")]
        public void FailArgsUpdate() => UpdateTitle(FailingTitle);

        private void UpdateTitle(string title) => Run(() => UpdateTitleAsync(title));

        private async Task UpdateTitleAsync(string title)
        {
            if (initialized && host != null)
            {
                var outcome = await host.Navigator.UpdateArgsAsync(current.Identity, route, title);
                Debug.Log($"本地参数更新={outcome.Status}，清理={outcome.Cleanup}，视图故障={outcome.ViewFaulted}");
            }
        }

        [ContextMenu("本地换绑模型")]
        public void Rebind() => Run(RebindAsync);

        private async Task RebindAsync()
        {
            if (initialized && host != null)
            {
                // 新模型由示例借出；导航器不会将其当作工厂创建的自有模型。
                var next = new NavigationPageViewModel();
                var outcome = await host.Navigator.RebindAsync(current.Identity, route, next);
                Debug.Log($"本地换绑={outcome.Status}，清理={outcome.Cleanup}，视图故障={outcome.ViewFaulted}");
            }
        }

        [ContextMenu("本地替换当前页面")]
        public void Replace() => ReplaceCurrent("本地替换后的页面");

        [ContextMenu("本地替换准备失败并保留原页")]
        public void FailReplace() => ReplaceCurrent(FailingOpenTitle);

        private void ReplaceCurrent(string title) => Run(() => ReplaceCurrentAsync(title));

        private async Task ReplaceCurrentAsync(string title)
        {
            if (!initialized || host == null)
            {
                return;
            }
            var original = current;
            var outcome = await host.Navigator.ReplaceAsync(current.Identity, route, title);
            if (outcome.IsCommitted)
            {
                current = outcome.Destination.Handle;
                ObservePage(current);
            }
            Debug.Log($"本地替换={outcome.Status}，拒绝={outcome.Rejection}，原页状态={host.Navigator.GetState(original.Identity)}");
            if (outcome.SourceCleanup != null)
            {
                var sourceClose = await outcome.SourceCleanup;
                Debug.Log($"原页清理={sourceClose.Cleanup}，目标打开={outcome.Destination.Status}");
            }
        }

        [ContextMenu("本地打开批量关闭示例页")]
        public void OpenBatchPage() => Run(OpenBatchPageAsync);

        private async Task OpenBatchPageAsync()
        {
            if (initialized && host != null)
            {
                var outcome = await host.Navigator.OpenAsync(batchRoute, "命名层级批量关闭示例");
                ObservePage(outcome.Handle);
                Debug.Log($"批次页面打开={outcome.Status}，拒绝原因={outcome.Rejection}，超限替换={outcome.IsReplacement}");
                if (outcome.ReplacedCleanup != null)
                {
                    var replacedClose = await outcome.ReplacedCleanup;
                    Debug.Log($"最旧页面清理={replacedClose.Cleanup}");
                }
            }
        }

        [ContextMenu("本地关闭批次所在层")]
        public void CloseLayer() => Run(CloseLayerAsync);

        private async Task CloseLayerAsync()
        {
            if (initialized && host != null)
            {
                ReportBatch(await host.Navigator.CloseLayerAsync(batchRoute.Policy.Layer));
            }
        }

        [ContextMenu("本地关闭全部页面")]
        public void CloseAll() => Run(CloseAllAsync);

        private async Task CloseAllAsync()
        {
            if (initialized && host != null)
            {
                ReportBatch(await host.Navigator.CloseAllAsync());
            }
        }

        private static void ReportBatch(BatchCloseOutcome outcome)
        {
            Debug.Log($"本地批次={outcome.Status}，全部关闭={outcome.AllClosed}，项目数={outcome.Items.Count}");
            foreach (var item in outcome.Items)
            {
                if (item.Outcome.HasValue)
                {
                    var result = item.Outcome.Value;
                    Debug.Log($"页面={item.Handle}，关闭={result.Status}，清理={result.Cleanup}");
                }
            }
        }

        [ContextMenu("本地预加载与重复复用")]
        public void Preload() => Run(PreloadAsync);

        private async Task PreloadAsync()
        {
            if (initialized && host != null)
            {
                var outcome = await host.Navigator.PreloadAsync(route);
                Debug.Log($"本地预加载={outcome.Status}，驻留占用={host.Navigator.PreloadReservationCount}");
            }
        }

        [ContextMenu("本地清除预加载")]
        public void ClearPreloads() => Run(ClearPreloadsAsync);

        private async Task ClearPreloadsAsync()
        {
            if (initialized && host != null)
            {
                await host.Navigator.ClearPreloadsAsync();
                Debug.Log($"本地预加载清除，占用={host.Navigator.PreloadReservationCount}");
            }
        }

        [ContextMenu("本地清理闲置界面")]
        public void ClearInactiveContent() => Run(ClearInactiveContentAsync);

        private async Task ClearInactiveContentAsync()
        {
            if (initialized && host != null)
            {
                await host.ClearInactiveContentAsync();
            }
        }

        /// <summary>菜单入口统一观察异步失败；Unity 事件不丢弃未观察任务。</summary>
        private async void Run(Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private async void OnDestroy()
        {
            var shutdownHost = initialized && host != null;
            initialized = false;
            // 先撤销示例订阅，宿主关闭页面时不再向正在销毁的组件通知。
            try
            {
                await observationLifetime.DisposeAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            observedPages.Clear();
            if (shutdownHost && host != null)
            {
                try
                {
                    await host.ShutdownAsync();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
        }

        private sealed class PagePresenter : Presenter<NavigationPageViewModel, string, int>,
                    ICloseGuard, IArgsUpdatePresenter<string>
        {
            private readonly LocalNavigationDemo owner;

            public PagePresenter(LocalNavigationDemo owner) => this.owner = owner;

            public long CloseVersion => owner.allowClose ? 1 : 0;

            public ValueTask<CloseDecision> CanCloseAsync(CloseContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<CloseDecision>(owner.allowClose ? CloseDecision.Allow : CloseDecision.Deny);
            }

            protected override void OnCreate()
            {
                ++owner.createdPages;
                InstanceLifetime.OnDispose(() => Debug.Log("本地实例资源已释放"));
            }

            protected override void OnViewModelChanged(NavigationPageViewModel previous)
            {
                ViewModel.Title = Context.Args;
            }

            public ValueTask<IPreparedArgsUpdate> PrepareArgsUpdateAsync(string args, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    throw new ArgumentException("页面标题不能为空。", nameof(args));
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<IPreparedArgsUpdate>(new PreparedTitle(ViewModel, args));
            }

            protected override void OnOpen(string title)
            {
                if (title == FailingOpenTitle)
                {
                    throw new InvalidOperationException("示例候选准备失败，原页面应保持打开。");
                }
                ViewModel.Title = title;
                Context.Scope.OnDispose(() => Debug.Log("本地激活资源已释放"));
            }

            private sealed class PreparedTitle : IPreparedArgsUpdate
            {
                private NavigationPageViewModel model;
                private readonly string next;

                public PreparedTitle(NavigationPageViewModel model, string next)
                {
                    this.model = model;
                    this.next = next;
                }

                public void Commit()
                {
                    model.Title = next;
                    if (next == FailingTitle)
                    {
                        throw new InvalidOperationException("示例在写入标题后故意失败，框架应撤销输入并故障关闭。");
                    }
                }

                public ValueTask DisposeAsync()
                {
                    // 候选仅借用模型，释放时不销毁业务模型。
                    model = null;
                    return default;
                }
            }
        }
    }
}
