using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>异步共享依赖：延迟加载、准备失败后的物理回滚、取消及可选父页面保留。</summary>
    public sealed partial class AsynchronousDependenciesDemo : MonoBehaviour
    {
        [SerializeField] private UIHost host = null;
        [SerializeField] private GameObject pagePrefab = null;
        [SerializeField] private GameObject dependencyPrefab = null;
        [SerializeField, Min(0)] private int loadDelayMilliseconds = 500;
        [SerializeField, Min(0)] private int prepareDelayMilliseconds = 1000;
        [SerializeField, Min(0)] private int cleanupDelayMilliseconds = 500;
        [SerializeField] private bool simulateNonCooperativeLoading = false;
        private Route<NavigationPageViewModel, string, int> optionalRoute;
        private Route<NavigationPageViewModel, string, int> failureRoute;
        private ViewHandle<int> optionalParent;
        private ViewHandle<int> failureParent;
        private ViewHandle shared;
        private CancellationTokenSource openCancellation;
        private Task opening;
        private Task closingShared;
        private Navigator navigator;
        private bool disposed;

        private void Start()
        {
            if (host == null || pagePrefab == null || dependencyPrefab == null)
            {
                Debug.LogError("请配置独立 UIHost、NavigationPage 和 ThingItem Prefab。", this);
                return;
            }
            var page = new ViewResource("AsyncDependencyParent");
            var item = new ViewResource("AsyncSharedDependency");
            var provider = new DelayedProvider(new PrefabViewProvider(host.transform,
                new[]
                {
                    new KeyValuePair<ViewResource, GameObject>(page, pagePrefab),
                    new KeyValuePair<ViewResource, GameObject>(item, dependencyPrefab)
                }), Math.Max(0, loadDelayMilliseconds), simulateNonCooperativeLoading);
            try
            {
                host.Initialize(provider, ownsProvider: true);
            }
            catch (Exception error)
            {
                provider.Dispose();
                Debug.LogException(error, this);
                return;
            }
            navigator = host.Navigator;
            var prepareDelay = Math.Max(0, prepareDelayMilliseconds);
            var cleanupDelay = Math.Max(0, cleanupDelayMilliseconds);
            var sharedRoute = CreateDependency("demo.async.shared", item, false, prepareDelay, cleanupDelay);
            var failingRoute = CreateDependency("demo.async.failing", item, true, prepareDelay, cleanupDelay);
            optionalRoute = CreatePage("demo.async.optional-parent", page,
                new[] { RouteDependency<string>.Optional(sharedRoute) });
            failureRoute = CreatePage("demo.async.failure-parent", page,
                new[]
                {
                    RouteDependency<string>.Required(sharedRoute),
                    RouteDependency<string>.Optional(failingRoute, DependencyPlacement.AttachedAfter)
                });
            navigator.LifecycleChanged += OnLifecycleChanged;
        }

        private Route<NavigationPageViewModel, string, int> CreatePage(string key, ViewResource page,
                    IReadOnlyList<RouteDependency<string>> dependencies) =>
                    new Route<NavigationPageViewModel, string, int>(key, page,
                        () => new NavigationPageViewModel(), presenterFactory: _ => new PagePresenter(),
                        bindingFactory: NavigationPageViewModelBindingFactory.Create, policy: host.ResolvePolicy(), dependencies: dependencies);

        [ContextMenu("异步打开两个依赖父页面")]
        public void OpenParents()
        {
            if (disposed || navigator == null || (opening != null && !opening.IsCompleted))
            {
                return;
            }
            var cancellation = new CancellationTokenSource();
            openCancellation = cancellation;
            opening = OpenParentsAsync(cancellation);
        }

        private async Task OpenParentsAsync(CancellationTokenSource cancellation)
        {
            try
            {
                var optional = await navigator.OpenAsync(optionalRoute, "可选共享依赖退出后保留", cancellation.Token);
                optionalParent = optional.Handle;
                Debug.Log($"异步可选父打开={optional.Status}");
                if (!optional.IsSuccess || cancellation.IsCancellationRequested || disposed)
                {
                    return;
                }
                var dependencies = navigator.GetDependencies(optional.Handle.Identity);
                shared = dependencies.Count == 0 ? default : dependencies[0];
                var failed = await navigator.OpenAsync(failureRoute, "可选准备失败，等回滚后显示", cancellation.Token);
                failureParent = failed.Handle;
                Debug.Log($"异步准备失败父打开={failed.Status}，错误={failed.Error}");
                ReportParents();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                if (ReferenceEquals(openCancellation, cancellation))
                {
                    openCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        [ContextMenu("取消当前异步打开")]
        public void CancelOpen()
        {
            // 取消只影响当前打开请求，已经提交的其他页面继续保留。
            try
            {
                openCancellation?.Cancel();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        [ContextMenu("异步强制关闭共享依赖")]
        public void ForceCloseShared()
        {
            if (disposed || navigator == null || !shared.IsValid ||
                (closingShared != null && !closingShared.IsCompleted))
            {
                return;
            }
            closingShared = ForceCloseSharedAsync();
        }

        private async Task ForceCloseSharedAsync()
        {
            try
            {
                var closed = await navigator.ForceCloseAsync(shared);
                Debug.Log($"共享依赖强制关闭={closed.Status}，清理={closed.Cleanup}");
                ReportParents();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private void OnLifecycleChanged(NavigationEvent change)
        {
            if (change.DependencyFailure.HasValue)
            {
                var failure = change.DependencyFailure.Value;
                Debug.LogWarning($"异步父页面 {change.Route.Key} 降级：{failure.Target.Key}/{failure.Stage}/{failure.Error.Message}");
            }
        }

        private void ReportParents()
        {
            if (disposed)
            {
                return;
            }
            foreach (var parent in new[] { optionalParent, failureParent })
            {
                if (parent.IsValid)
                {
                    Debug.Log($"父状态={navigator.GetState(parent.Identity)}，降级数={navigator.GetDependencyFailures(parent.Identity).Count}，首次就绪={parent.Readiness.Status}/{parent.Readiness.IsDegraded}");
                }
            }
        }

        private void OnDestroy()
        {
            disposed = true;
            CancelOpen();
            if (navigator != null)
            {
                navigator.LifecycleChanged -= OnLifecycleChanged;
                if (host != null)
                {
                    _ = ObserveShutdownAsync(host.ShutdownAsync());
                }
            }
        }

        private static async Task ObserveShutdownAsync(ValueTask completion)
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

        private sealed class PagePresenter : Presenter<NavigationPageViewModel, string, int>
        {
            protected override void OnOpen(string args) => ViewModel.Title = args;
        }
    }
}
