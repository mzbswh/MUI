using System;
using System.Collections.Generic;
using MUI.ChildViews;
using MUI.Resources;
using MUI.Tabs;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Tabs
{
    /// <summary>纯同步 Tab 示例，包含同步守卫、缓存复用、失败保留及帧驱动过期回收。</summary>
    public sealed class SynchronousTabsDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        [SerializeField] private TabBarElement tabBar = null;
        [SerializeField] private AsyncContentElement content = null;
        [SerializeField] private GameObject pagePrefab = null;
        [SerializeField] private bool allowLeave = true;
        [SerializeField] private bool failQuests = false;
        private readonly Lifetime lifetime = new Lifetime(LifetimeMode.Synchronous);
        private TabContentController controller;
        private TabContentDefinition[] definitions;
        private int createdModels;
        private bool maintenanceFailed;

        private void Start()
        {
            try
            {
                if (parentView == null || tabBar == null || content == null || pagePrefab == null)
                {
                    throw new InvalidOperationException("请配置父 View、兄弟区域的 TabBar 与内容控件，以及 TabPage Prefab。");
                }

                parentView.Initialize();
                parentView.SetHostState(false, false);
                var resource = new ViewResource("TabPage");
                var provider = lifetime.OwnDisposable(new PrefabViewProvider(parentView.transform,
                    new[] { new KeyValuePair<ViewResource, GameObject>(resource, pagePrefab) }));
                var mounted = content.CreateSynchronousProvider(provider);
                var template = new ChildViewTemplate<TabPageViewModel, string>(resource,
                    () =>
                    {
                        ++createdModels;
                        return new TabPageViewModel();
                    }, TabPageViewModelBindingFactory.Create, _ => new PagePresenter(), supportsSynchronousLifecycle: true);
                parentView.BeginChildActivation(lifetime);
                definitions = new[]
                {
                    TabContentDefinition.CreateSynchronous("inventory", "背包",
                        scope => scope.PrepareSynchronous(template, mounted, "同步背包"), canLeave: _ => allowLeave),
                    TabContentDefinition.CreateSynchronous("quests", "任务", scope =>
                    {
                        if (failQuests)
                        {
                            throw new InvalidOperationException("示例任务页准备失败。");
                        }

                        return scope.PrepareSynchronous(template, mounted, "同步任务");
                    })
                };
                controller = new TabContentController(parentView.ChildViews, definitions,
                    failureDisplay: TabFailureDisplay.RestorePrevious,
                    cacheOptions: new TabCacheOptions(TabContentRetention.CacheRecent, capacity: 2,
                        timeToLive: TimeSpan.FromSeconds(5)));
                tabBar.Bind(controller);
                content.Bind(controller);
                lifetime.OnDispose(() =>
                {
                    if (tabBar != null && tabBar.IsAlive)
                    {
                        tabBar.Bind(null);
                    }
                    if (content != null && content.IsAlive)
                    {
                        content.Bind(null);
                    }
                    if (parentView != null && parentView.IsAlive)
                    {
                        parentView.SetHostState(false, false);
                    }
                });
                Report(controller.Select("inventory"));
                parentView.CommitChildActivation();
                parentView.SetHostState(true, true);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Release();
            }
        }

        private void Update()
        {
            if (controller == null || lifetime.IsEnded || maintenanceFailed)
            {
                return;
            }

            try
            {
                // 由 Unity 帧驱动 TTL 检查，不启动定时任务；失败后停止重复报告。
                controller.RefreshCache();
            }
            catch (Exception error)
            {
                maintenanceFailed = true;
                Debug.LogException(error, this);
            }
        }

        [ContextMenu("同步选择背包")]
        public void SelectInventory()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                Report(controller.Select("inventory"));
            }
        }

        [ContextMenu("同步选择任务")]
        public void SelectQuests()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                Report(controller.Select("quests", forceReload: failQuests));
            }
        }

        [ContextMenu("同步清理缓存")]
        public void ClearCache()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                controller.ClearCache();
            }
        }

        [ContextMenu("同步清理页签缓存")]
        public void ClearInactiveContent()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                controller.ClearCache();
            }
        }

        [ContextMenu("同步移除任务定义")]
        public void RemoveQuests()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                Report(controller.UpdateDefinitions(new[] { definitions[0] }));
            }
        }

        [ContextMenu("同步恢复全部定义")]
        public void RestoreDefinitions()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                Report(controller.UpdateDefinitions(definitions));
            }
        }

        private void Report(TabSelectionResult result)
        {
            Debug.Log($"同步 Tab：{result.Status}/{result.Rejection}，显示={controller.ViewModel.Snapshot.DisplayedTab}，创建模型={createdModels}，缓存={controller.CachedContentCount}", this);
            if (result.Error != null)
            {
                Debug.LogException(result.Error, this);
            }
        }

        private void OnDestroy() => Release();

        private void Release()
        {
            try
            {
                lifetime.Dispose();
                controller = null;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        private sealed class PagePresenter : Presenter<TabPageViewModel, string, Unit>
        {
            protected override void OnOpen(string title) => ViewModel.Title = title;
        }
    }
}
