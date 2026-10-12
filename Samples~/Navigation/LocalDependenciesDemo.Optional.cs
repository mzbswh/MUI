using MUI.Pages.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MUI.Pages;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class LocalDependenciesDemo
    {
        private PageDefinition<NavigationPageViewModel, string, int> optionalRoute;
        private PageDefinition<NavigationPageViewModel, string, int> optionalFailureRoute;
        private PageHandle<int> optionalParent;
        private PageHandle<int> optionalFailureParent;

        /// <summary>供项目编辑器显式采集静态声明，不创建界面；未初始化时返回空集合。</summary>
        public IReadOnlyList<PageDefinition> GetDiagnosticRoutes()
        {
            if (!initialized || optionalRoute == null || optionalFailureRoute == null)
            {
                return Array.Empty<PageDefinition>();
            }
            return new PageDefinition[] { firstRoute, secondRoute, conflictingRoute, optionalRoute, optionalFailureRoute };
        }

        private void InitializeOptionalRoutes(ViewResource page, ViewResource item)
        {
            var failing = new PageDefinition<ThingItemViewModel, Unit, Unit>("demo.shared.optional-failure", item,
                () => new ThingItemViewModel { Label = "准备失败的可选界面" },
                presenterFactory: _ => new FailingOptionalPresenter(),
                bindingFactory: ThingItemViewModelBindingFactory.Create,
                policy: ResolveDependencyPolicy());
            optionalRoute = CreatePage("demo.shared.optional-parent", "允许共享依赖退出的父页面", page,
                new[] { PageDependency<string>.Optional(sharedRoute, _ => "default", sharedPlacement) });
            optionalFailureRoute = CreatePage("demo.shared.optional-failure-parent", "可选准备失败仍保留的父页面", page,
                new[]
                {
                    PageDependency<string>.Required(sharedRoute, _ => "default", sharedPlacement),
                    PageDependency<string>.Optional(failing, DependencyPlacement.AttachedAfter)
                });
        }

        [ContextMenu("本地打开可选依赖示例")]
        public void OpenOptionalParents() => Run(OpenOptionalParentsAsync);

        private async Task OpenOptionalParentsAsync()
        {
            if (!initialized || host == null)
            {
                return;
            }
            var optional = await host.Pages.OpenAsync(optionalRoute, "依赖强制退出后仍保留此父页面");
            optionalParent = optional.Handle;
            var failed = await host.Pages.OpenAsync(optionalFailureRoute, "可选准备失败，必需兄弟依赖仍保留");
            optionalFailureParent = failed.Handle;
            Debug.Log($"可选依赖示例：共享父打开={optional.Status}，准备失败父打开={failed.Status}");
            if (failed.Handle.IsValid)
            {
                Debug.Log($"准备失败父就绪={failed.Handle.Readiness.Status}，已降级={failed.Handle.Readiness.IsDegraded}");
            }
            ReportOwners();
        }

        [ContextMenu("本地更新已降级父页面标题")]
        public void UpdateDegradedParentTitle() => Run(UpdateDegradedParentTitleAsync);

        private async Task UpdateDegradedParentTitleAsync()
        {
            if (initialized && host != null && optionalFailureParent.IsValid)
            {
                var result = await host.Pages.UpdateArgsAsync(optionalFailureParent.Identity, optionalFailureRoute, "已更新标题，保持可选依赖缺席");
                Debug.Log($"已降级父参数更新={result.Status}");
                ReportOptionalParents();
            }
        }

        private void OnDependencyLifecycleChanged(PageEvent change)
        {
            if (change.Kind == PageEventKind.DependencyDegraded && change.DependencyFailure.HasValue)
            {
                var failure = change.DependencyFailure.Value;
                Debug.LogWarning($"父页面 {change.Definition.Key} 降级：依赖={failure.Target.Key}，阶段={failure.Stage}，原因={failure.Error.Message}");
            }
        }

        private void ReportOptionalParents()
        {
            foreach (var handle in new[] { optionalParent, optionalFailureParent })
            {
                if (handle.IsValid)
                {
                    Debug.Log($"可选示例父状态={host.Pages.GetState(handle.Identity)}，当前依赖数={host.Pages.GetDependencies(handle.Identity).Count}，降级数={host.Pages.GetDependencyFailures(handle.Identity).Count}");
                }
            }
        }

        /// <summary>已经取得视图后失败，演示回滚绑定、激活资源与实例资源再继续父页面。</summary>
        private sealed class FailingOptionalPresenter : Presenter<ThingItemViewModel, Unit, Unit>
        {
            protected override void OnCreate() =>
                            InstanceLifetime.OnDispose(() => Debug.Log("可选依赖的实例资源已本地释放"));

            protected override void OnOpen(Unit args)
            {
                Context.Scope.OnDispose(() => Debug.Log("可选依赖的激活资源已本地释放"));
                throw new InvalidOperationException("演示可选依赖已加载 Prefab 后准备失败。");
            }
        }
    }
}
