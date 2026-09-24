using System;
using System.Collections.Generic;
using MUI.Navigation;
using MUI.Resources;
using MUI.Samples.ResourceIntegration;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>纯同步顶层导航示例：打开、结果、守卫、缓存、返回、低内存及宿主释放。</summary>
    public sealed partial class SynchronousNavigationDemo : MonoBehaviour
    {
        private const string FailingOpenTitle = "演示同步替换准备失败";
        private const string FailingTitle = "演示同步提交失败";
        [SerializeField] private UIHost host = null;
        [SerializeField] private GameObject pagePrefab = null;
        [SerializeField] private string resourcesPath = string.Empty;
        [SerializeField] private bool allowClose = true;
        private Route<SynchronousNavigationViewModel, string, int> route;
        private Route<SynchronousNavigationViewModel, string, int> batchRoute;
        private ViewHandle<int> current;
        private int createdPages;
        private bool initialized;
        private readonly Lifetime observationLifetime = new Lifetime(LifetimeMode.Synchronous);
        private readonly HashSet<ViewHandle> observedPages = new HashSet<ViewHandle>();

        private void Start()
        {
            try
            {
                if (host == null || (pagePrefab == null && string.IsNullOrWhiteSpace(resourcesPath)))
                {
                    throw new InvalidOperationException("请配置 UIHost，并指定 Prefab 或 Resources 相对路径。");
                }

                var resource = new ViewResource("SynchronousNavigationView");
                ISynchronousViewProvider provider;
                if (!string.IsNullOrWhiteSpace(resourcesPath))
                {
                    var path = resourcesPath;
                    var loader = new MUI.Samples.ResourceIntegration.UnityResourcesLoader();
                    provider = new SynchronousLoadedPrefabViewProvider(host.transform, loader, _ => path,
                        configureView: view => view.ConfigureSynchronousResources(loader));
                }
                else
                {
                    provider = new PrefabViewProvider(host.transform,
                        new[] { new KeyValuePair<ViewResource, GameObject>(resource, pagePrefab) });
                }
                try
                {
                    host.InitializeSynchronous(provider, ownsProvider: true, cacheCapacity: 2);
                }
                catch
                {
                    ((IDisposable)provider).Dispose();
                    throw;
                }

                initialized = true;
                route = SynchronousNavigationViewModelRoute.Create(
                    () => new SynchronousNavigationViewModel(), _ => new PagePresenter(this),
                    policy: new RoutePolicy(cacheMode: ViewCacheMode.KeepAlive));
                batchRoute = SynchronousNavigationViewModelRoute.Create(
                    () => new SynchronousNavigationViewModel(), _ => new PagePresenter(this),
                    policy: new RoutePolicy(layer: 1, allowMultiple: true, maxInstances: 3,
                        overflow: OverflowPolicy.CloseOldest), key: "demo.pure-sync.batch");
                Open();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        [ContextMenu("同步打开或缓存重开")]
        public void Open()
        {
            if (!initialized || host == null)
            {
                return;
            }
            var result = host.Navigator.Open(route, "完全同步的顶层界面");
            current = result.Handle;
            ObservePage(current);
            Debug.Log($"同步打开={result.Status}，实际创建次数={createdPages}，缓存数={host.Navigator.CachedViewCount}");
        }

        [ContextMenu("同步关闭")]
        public void Close()
        {
            if (initialized && host != null)
            {
                Debug.Log($"同步关闭={host.Navigator.Close(current).Status}");
                ReadResult();
            }
        }

        [ContextMenu("同步返回")]
        public void Back()
        {
            if (initialized && host != null)
            {
                Debug.Log($"同步返回={host.Navigator.Back().Status}");
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
                    Debug.Log($"同步页面={handle.Identity}，就绪={readiness.Status}"));
                handle.ObserveResult(observationLifetime, result =>
                {
                    observedPages.Remove(handle.Identity);
                    Debug.Log($"同步页面={handle.Identity}，结果={result.Status}，值={result.Value}，清理={result.Cleanup}");
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

        [ContextMenu("同步更新参数")]
        public void UpdateArgs() => UpdateTitle("同步更新后的标题");

        [ContextMenu("同步参数提交失败并回滚")]
        public void FailArgsUpdate() => UpdateTitle(FailingTitle);

        private void UpdateTitle(string title)
        {
            if (initialized && host != null)
            {
                var outcome = host.Navigator.UpdateArgs(current.Identity, route, title);
                Debug.Log($"同步参数更新={outcome.Status}，清理={outcome.Cleanup}，恢复失败={outcome.RecoveryFailed}");
            }
        }

        [ContextMenu("同步换绑模型")]
        public void Rebind()
        {
            if (initialized && host != null)
            {
                // 新模型由示例借出；导航器不会将其当作工厂创建的自有模型。
                var next = new SynchronousNavigationViewModel();
                var outcome = host.Navigator.Rebind(current.Identity, route, next);
                Debug.Log($"同步换绑={outcome.Status}，清理={outcome.Cleanup}，恢复失败={outcome.RecoveryFailed}");
            }
        }

        [ContextMenu("同步替换当前页面")]
        public void Replace() => ReplaceCurrent("同步替换后的页面");

        [ContextMenu("同步替换准备失败并保留原页")]
        public void FailReplace() => ReplaceCurrent(FailingOpenTitle);

        private void ReplaceCurrent(string title)
        {
            if (!initialized || host == null)
            {
                return;
            }
            var original = current;
            var outcome = host.Navigator.Replace(current.Identity, route, title);
            if (outcome.IsCommitted)
            {
                current = outcome.Destination.Handle;
                ObservePage(current);
            }
            Debug.Log($"同步替换={outcome.Status}，拒绝={outcome.Rejection}，原页状态={host.Navigator.GetState(original.Identity)}");
            if (outcome.SourceClose.HasValue)
            {
                Debug.Log($"原页清理={outcome.SourceClose.Value.Cleanup}，目标打开={outcome.Destination.Status}");
            }
        }

        [ContextMenu("同步打开批量关闭示例页")]
        public void OpenBatchPage()
        {
            if (initialized && host != null)
            {
                var outcome = host.Navigator.Open(batchRoute, "第 1 层批量关闭示例");
                ObservePage(outcome.Handle);
                Debug.Log($"批次页面打开={outcome.Status}，拒绝原因={outcome.Rejection}，超限替换={outcome.IsReplacement}");
                if (outcome.ReplacedClose.HasValue)
                {
                    Debug.Log($"最旧页面清理={outcome.ReplacedClose.Value.Cleanup}");
                }
            }
        }

        [ContextMenu("同步关闭第 1 层")]
        public void CloseLayer()
        {
            if (initialized && host != null)
            {
                ReportBatch(host.Navigator.CloseLayer(1));
            }
        }

        [ContextMenu("同步关闭全部页面")]
        public void CloseAll()
        {
            if (initialized && host != null)
            {
                ReportBatch(host.Navigator.CloseAll());
            }
        }

        private static void ReportBatch(BatchCloseOutcome outcome)
        {
            Debug.Log($"同步批次={outcome.Status}，全部关闭={outcome.AllClosed}，项目数={outcome.Items.Count}");
            foreach (var item in outcome.Items)
            {
                if (item.Outcome.HasValue)
                {
                    var result = item.Outcome.Value;
                    Debug.Log($"页面={item.Handle}，关闭={result.Status}，清理={result.Cleanup}");
                }
            }
        }

        [ContextMenu("同步预加载与重复复用")]
        public void Preload()
        {
            if (initialized && host != null)
            {
                var outcome = host.Navigator.Preload(route);
                Debug.Log($"同步预加载={outcome.Status}，驻留占用={host.Navigator.PreloadReservationCount}");
            }
        }

        [ContextMenu("同步清除预加载")]
        public void ClearPreloads()
        {
            if (initialized && host != null)
            {
                host.Navigator.ClearPreloads();
                Debug.Log($"同步预加载清除，占用={host.Navigator.PreloadReservationCount}");
            }
        }

        [ContextMenu("同步清理闲置界面")]
        public void ClearInactiveContent()
        {
            if (initialized && host != null)
            {
                host.ClearInactiveContent();
            }
        }

        private void OnDestroy()
        {
            // 先撤销示例订阅，宿主关闭页面时不再向正在销毁的组件通知。
            try
            {
                observationLifetime.Dispose();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            observedPages.Clear();
            if (initialized && host != null)
            {
                try
                {
                    host.Shutdown();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
        }

        private sealed class PagePresenter : Presenter<SynchronousNavigationViewModel, string, int>,
                    IReusableViewPresenter, ISynchronousCloseGuard, ISynchronousArgsUpdatePresenter<string>
        {
            private readonly SynchronousNavigationDemo owner;

            public PagePresenter(SynchronousNavigationDemo owner) => this.owner = owner;

            public long CloseVersion => owner.allowClose ? 1 : 0;

            public bool CanClose(CloseContext context) => owner.allowClose;

            protected override void OnCreate()
            {
                ++owner.createdPages;
                InstanceLifetime.OnDispose(() => Debug.Log("同步实例资源已释放"));
            }

            protected override void OnViewModelChanged(SynchronousNavigationViewModel previous)
            {
                ViewModel.Title = Context.Args;
            }

            public ISynchronousPreparedArgsUpdate PrepareArgsUpdate(string args)
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    throw new ArgumentException("页面标题不能为空。", nameof(args));
                }
                return new PreparedTitle(ViewModel, args);
            }

            protected override void OnOpen(string title)
            {
                if (title == FailingOpenTitle)
                {
                    throw new InvalidOperationException("示例候选准备失败，原页面应保持打开。");
                }
                ViewModel.Title = title;
                Context.Lifetime.OnDispose(() => Debug.Log("同步激活资源已释放"));
            }

            private sealed class PreparedTitle : ISynchronousPreparedArgsUpdate
            {
                private SynchronousNavigationViewModel model;
                private readonly string previous;
                private readonly string next;

                public PreparedTitle(SynchronousNavigationViewModel model, string next)
                {
                    this.model = model;
                    previous = model.Title;
                    this.next = next;
                }

                public void Commit()
                {
                    model.Title = next;
                    if (next == FailingTitle)
                    {
                        throw new InvalidOperationException("示例在写入标题后故意失败，框架应恢复旧标题和参数。");
                    }
                }

                public void Rollback() => model.Title = previous;

                public void Dispose()
                {
                    // 候选仅借用模型，释放时不销毁业务模型。
                    model = null;
                }
            }
        }
    }
}
