using System;
using System.Collections.Generic;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>两个父页面共享一个必需界面，演示同步取得、关闭保护和末拥有者释放。</summary>
    public sealed partial class SynchronousDependenciesDemo : MonoBehaviour
    {
        private const string DependencyChangeTitle = "演示依赖参数变化";
        [SerializeField] private UIHost host = null;
        [SerializeField] private GameObject pagePrefab = null;
        [SerializeField] private GameObject dependencyPrefab = null;
        [SerializeField] private DependencyPlacement sharedPlacement = DependencyPlacement.RequiredBefore;
        private Route<ThingItemViewModel, string, Unit> sharedRoute;
        private Route<SynchronousNavigationViewModel, string, int> firstRoute;
        private Route<SynchronousNavigationViewModel, string, int> secondRoute;
        private Route<SynchronousNavigationViewModel, string, int> conflictingRoute;
        private Route<SynchronousNavigationViewModel, string, int> independentRoute;
        private ViewHandle<int> independent;
        private ViewHandle<int> first;
        private ViewHandle<int> second;
        private ViewHandle shared;
        private bool initialized;
        private int sharedCreated;

        private void Start()
        {
            if (host == null || pagePrefab == null || dependencyPrefab == null)
            {
                Debug.LogError("请配置 UIHost、同步导航页面 Prefab 和 ThingItem 依赖 Prefab。", this);
                return;
            }
            try
            {
                var page = new ViewResource("SynchronousNavigationView");
                var item = new ViewResource("SynchronousSharedDependency");
                var provider = new PrefabViewProvider(host.transform,
                    new[]
                    {
                        new KeyValuePair<ViewResource, GameObject>(page, pagePrefab),
                        new KeyValuePair<ViewResource, GameObject>(item, dependencyPrefab)
                    });
                try
                {
                    host.InitializeSynchronous(provider, ownsProvider: true);
                }
                catch
                {
                    provider.Dispose();
                    throw;
                }
                initialized = true;
                sharedRoute = new Route<ThingItemViewModel, string, Unit>("demo.shared-status", item,
                    () => new ThingItemViewModel { Label = $"共享状态，第 {++sharedCreated} 次创建" },
                    bindingFactory: ThingItemViewModelBindingFactory.Create,
                    policy: new RoutePolicy(layer: 0, enterHistory: false, takesFocus: false,
                        backBehavior: BackBehavior.Ignore), supportsSynchronousLifecycle: true);
                var dependencies = new[] { RouteDependency<string>.Required(sharedRoute,
                    title => title == DependencyChangeTitle ? "other" : "default", sharedPlacement) };
                firstRoute = CreatePage("demo.shared.first", "第一个父页面", page, dependencies);
                secondRoute = CreatePage("demo.shared.second", "第二个父页面", page, dependencies);
                conflictingRoute = CreatePage("demo.shared.conflicting", "参数冲突的父页面", page,
                    new[] { RouteDependency<string>.Required(sharedRoute, _ => "other", sharedPlacement) });
                // 不同业务上下文使用显式路由变体，共用 Prefab，但不共用正在服务其他父页面的实例。
                var independentDependency = new Route<ThingItemViewModel, string, Unit>(
                    "demo.independent-status", item,
                    () => new ThingItemViewModel { Label = "独立上下文：other" },
                    bindingFactory: ThingItemViewModelBindingFactory.Create,
                    policy: new RoutePolicy(layer: 0, enterHistory: false, takesFocus: false,
                        backBehavior: BackBehavior.Ignore), supportsSynchronousLifecycle: true);
                independentRoute = CreatePage("demo.independent.parent", "独立参数的父页面", page,
                    new[] { RouteDependency<string>.Required(independentDependency, _ => "other", sharedPlacement) });
                InitializeOptionalRoutes(page, item);
                host.Navigator.LifecycleChanged += OnDependencyLifecycleChanged;
                OpenBoth();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        private static Route<SynchronousNavigationViewModel, string, int> CreatePage(string key, string title,
                    ViewResource resource, IReadOnlyList<RouteDependency<string>> dependencies) =>
                    new Route<SynchronousNavigationViewModel, string, int>(key, resource,
                        () => new SynchronousNavigationViewModel { Title = title },
                        presenterFactory: _ => new PagePresenter(),
                        bindingFactory: SynchronousNavigationViewModelBindingFactory.Create,
                        supportsSynchronousLifecycle: true, dependencies: dependencies);

        [ContextMenu("校验共享示例的静态路由图")]
        public void ValidateRoutes()
        {
            if (!initialized || firstRoute == null || secondRoute == null)
            {
                return;
            }
            foreach (Route route in new Route[] { firstRoute, secondRoute, conflictingRoute, independentRoute })
            {
                var validation = RouteGraphValidator.Validate(route);
                Debug.Log($"路由图={route.Key}，有效={validation.IsValid}，路由数={validation.RouteCount}");
                foreach (var issue in validation.Issues)
                {
                    Debug.LogWarning($"{issue.RouteKey}: {issue.Rejection}，{issue.Message}");
                }
            }
        }

        [ContextMenu("同步打开两个父页面")]
        public void OpenBoth()
        {
            if (!initialized || host == null)
            {
                return;
            }
            var a = host.Navigator.Open(firstRoute, "第一个父页面");
            var b = host.Navigator.Open(secondRoute, "第二个父页面");
            first = a.Handle;
            second = b.Handle;
            var dependencies = host.Navigator.GetDependencies(first.Identity);
            shared = dependencies.Count == 0 ? default : dependencies[0];
            Debug.Log($"父页面打开={a.Status}/{b.Status}，共享创建次数={sharedCreated}");
            ReportOwners();
        }

        [ContextMenu("显式打开共享依赖并独立持有")]
        public void OpenSharedExplicitly()
        {
            if (initialized && host != null)
            {
                var result = host.Navigator.Open(sharedRoute, "default");
                if (result.IsSuccess)
                {
                    shared = result.Handle.Identity;
                }
                Debug.Log($"共享界面显式打开={result.Status}");
                ReportOwners();
            }
        }

        [ContextMenu("撤销共享依赖的显式持有")]
        public void ReleaseSharedExplicitly()
        {
            if (initialized && host != null)
            {
                var result = host.Navigator.ReleaseExplicitOwnership(shared);
                Debug.Log($"显式持有撤销={result.Status}，已撤销={result.IsReleased}，涉及关闭={result.CloseOutcome.HasValue}");
                ReportOwners();
            }
        }

        [ContextMenu("尝试打开依赖参数冲突的父页面")]
        public void OpenConflictingParent()
        {
            if (initialized && host != null)
            {
                var result = host.Navigator.Open(conflictingRoute, "依赖参数冲突的父页面");
                Debug.Log($"冲突页面打开={result.Status}，拒绝={result.Rejection}，清理={result.Cleanup}");
                ReportOwners();
            }
        }

        [ContextMenu("尝试单独关闭共享依赖")]
        public void CloseShared()
        {
            if (initialized && host != null)
            {
                Debug.Log($"共享依赖关闭={host.Navigator.Close(shared).Status}，有父拥有者时应为 InUse");
            }
        }

        [ContextMenu("强制关闭共享依赖及全部父页面")]
        public void ForceCloseShared()
        {
            if (initialized && host != null)
            {
                Debug.Log($"共享依赖强制关闭={host.Navigator.ForceClose(shared).Status}");
                Debug.Log($"父页面状态={host.Navigator.GetState(first.Identity)}/{host.Navigator.GetState(second.Identity)}");
                ReportOwners();
            }
        }

        [ContextMenu("同步更新父标题并保留依赖参数")]
        public void UpdateFirstTitle() => UpdateFirstArgs("更新后的父页面标题");

        [ContextMenu("尝试通过父参数改写共享依赖")]
        public void ChangeDependencyArgs() => UpdateFirstArgs(DependencyChangeTitle);

        private void UpdateFirstArgs(string title)
        {
            if (initialized && host != null && first.IsValid)
            {
                var result = host.Navigator.UpdateArgs(first.Identity, firstRoute, title);
                Debug.Log($"父参数更新={result.Status}，拒绝={result.Rejection}，共享创建次数={sharedCreated}");
                ReportOwners();
            }
        }

        [ContextMenu("以独立依赖变体打开不同参数的父页面")]
        public void OpenIndependentVariant()
        {
            if (!initialized || host == null)
            {
                return;
            }

            var opened = host.Navigator.Open(independentRoute, "独立参数的父页面");
            if (opened.IsSuccess)
            {
                independent = opened.Handle;
                var dependencies = host.Navigator.GetDependencies(independent.Identity);
                var dependency = dependencies.Count == 0 ? default : dependencies[0];
                Debug.Log($"独立变体={opened.Status}，独立依赖={dependency}，原共享依赖={shared}");
            }
            else
            {
                Debug.Log($"独立变体打开失败：{opened.Status}/{opened.Rejection}");
            }
            ReportOwners();
        }

        [ContextMenu("同步关闭独立变体父页面")]
        public void CloseIndependentVariant() => CloseParent(independent);

        [ContextMenu("同步换绑第一个父页面并保留共享依赖")]
        public void RebindFirst()
        {
            if (initialized && host != null && first.IsValid)
            {
                var result = host.Navigator.Rebind(first.Identity, firstRoute,
                    new SynchronousNavigationViewModel { Title = "换绑后的父页面" });
                Debug.Log($"父页面换绑={result.Status}，拒绝={result.Rejection}，共享创建次数={sharedCreated}");
                ReportOwners();
            }
        }

        [ContextMenu("尝试换绑被共享的依赖模型")]
        public void RebindShared()
        {
            if (initialized && host != null)
            {
                var result = host.Navigator.Rebind(shared, sharedRoute,
                    new ThingItemViewModel { Label = "替换共享模型" });
                Debug.Log($"依赖换绑={result.Status}，拒绝={result.Rejection}");
            }
        }

        [ContextMenu("同步关闭第一个父页面")]
        public void CloseFirst() => CloseParent(first);

        [ContextMenu("同步关闭第二个父页面")]
        public void CloseSecond() => CloseParent(second);

        private void CloseParent(ViewHandle<int> parent)
        {
            if (initialized && host != null && parent.IsValid)
            {
                Debug.Log($"父页面关闭={host.Navigator.Close(parent).Status}");
                ReportOwners();
            }
        }

        private void ReportOwners()
        {
            ReportOptionalParents();
            Debug.Log($"共享父拥有者数={host.Navigator.GetOwners(shared).Count}，显式持有={host.Navigator.HasExplicitOwnership(shared)}，共享状态={host.Navigator.GetState(shared)}");
        }

        private void OnDestroy()
        {
            if (initialized && host != null)
            {
                try
                {
                    host.Navigator.LifecycleChanged -= OnDependencyLifecycleChanged;
                    host.Shutdown();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
        }

        private sealed class PagePresenter : Presenter<SynchronousNavigationViewModel, string, int>,
                    ISynchronousArgsUpdatePresenter<string>
        {
            protected override void OnOpen(string args) => ViewModel.Title = args;

            protected override void OnViewModelChanged(SynchronousNavigationViewModel previous) =>
                            ViewModel.Title = Context.Args;

            public ISynchronousPreparedArgsUpdate PrepareArgsUpdate(string args) => new TitleUpdate(ViewModel, args);
        }

        /// <summary>只更新父标题，原值用于失败回滚；候选借用模型，不拥有模型生命周期。</summary>
        private sealed class TitleUpdate : ISynchronousPreparedArgsUpdate
        {
            private SynchronousNavigationViewModel model;
            private readonly string previous;
            private readonly string next;

            internal TitleUpdate(SynchronousNavigationViewModel model, string next)
            {
                this.model = model;
                previous = model.Title;
                this.next = next;
            }

            public void Commit() => model.Title = next;

            public void Rollback() => model.Title = previous;

            public void Dispose() => model = null;
        }
    }
}
