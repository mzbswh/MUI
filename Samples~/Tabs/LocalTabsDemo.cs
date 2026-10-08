using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Resources;
using MUI.Tabs;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Tabs
{
    /// <summary>本地 Prefab 的 Tab 示例；与异步加载共用准备、守卫、缓存和清理协议。</summary>
    public sealed class LocalTabsDemo : MonoBehaviour
    {
        [SerializeField] private View parentView = null;
        [SerializeField] private TabBarElement tabBar = null;
        [SerializeField] private AsyncContentElement content = null;
        [SerializeField] private GameObject pagePrefab = null;
        [SerializeField] private bool allowLeave = true;
        [SerializeField] private bool failQuests = false;
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private TabContentController controller;
        private TabContentDefinition[] definitions;
        private int createdModels;

        private async void Start()
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
                var mounted = content.CreateProvider(provider);
                var template = new ChildViewTemplate<TabPageViewModel, string>(resource,
                    () =>
                    {
                        ++createdModels;
                        return new TabPageViewModel();
                    }, TabPageViewModelBindingFactory.Create, _ => new PagePresenter());
                parentView.BeginChildActivation(lifetime);
                definitions = new[]
                {
                    new TabContentDefinition("inventory", "背包",
                        async (scope, token) => await scope.PrepareAsync(template, mounted, "背包", cancellationToken: token),
                        canLeaveAsync: (_, token) => new ValueTask<bool>(!token.IsCancellationRequested && allowLeave)),
                    new TabContentDefinition("quests", "任务", async (scope, token) =>
                    {
                        if (failQuests)
                        {
                            throw new InvalidOperationException("示例任务页准备失败。");
                        }

                        return await scope.PrepareAsync(template, mounted, "任务", cancellationToken: token);
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
                Report(await controller.SelectAsync("inventory", lifetime.Token));
                lifetime.Token.ThrowIfCancellationRequested();
                parentView.CommitChildActivation();
                parentView.SetHostState(true, true);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                await ReleaseAsync();
            }
        }

        [ContextMenu("选择背包")]
        public void SelectInventory() => Run(SelectInventoryAsync);

        private async Task SelectInventoryAsync()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                Report(await controller.SelectAsync("inventory", lifetime.Token));
            }
        }

        [ContextMenu("选择任务")]
        public void SelectQuests() => Run(SelectQuestsAsync);

        private async Task SelectQuestsAsync()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                Report(await controller.SelectAsync("quests", lifetime.Token, forceReload: failQuests));
            }
        }

        [ContextMenu("清理缓存")]
        public void ClearCache() => Run(ClearCacheAsync);

        private async Task ClearCacheAsync()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                await controller.ClearCacheAsync(lifetime.Token);
            }
        }

        [ContextMenu("清理页签缓存")]
        public void ClearInactiveContent() => Run(ClearInactiveContentAsync);

        private async Task ClearInactiveContentAsync()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                await controller.ClearCacheAsync(lifetime.Token);
            }
        }

        [ContextMenu("移除任务定义")]
        public void RemoveQuests() => Run(RemoveQuestsAsync);

        private async Task RemoveQuestsAsync()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                Report(await controller.UpdateDefinitionsAsync(new[] { definitions[0] }, cancellationToken: lifetime.Token));
            }
        }

        [ContextMenu("恢复全部定义")]
        public void RestoreDefinitions() => Run(RestoreDefinitionsAsync);

        private async Task RestoreDefinitionsAsync()
        {
            if (controller != null && !lifetime.IsEnded)
            {
                Report(await controller.UpdateDefinitionsAsync(definitions, cancellationToken: lifetime.Token));
            }
        }

        private void Report(TabSelectionResult result)
        {
            if (lifetime.IsEnded || controller == null)
            {
                return;
            }

            Debug.Log($"本地 Tab：{result.Status}/{result.Rejection}，显示={controller.ViewModel.Snapshot.DisplayedTab}，创建模型={createdModels}，缓存={controller.CachedContentCount}", this);
            if (result.Error != null)
            {
                Debug.LogException(result.Error, this);
            }
        }

        private async void Run(Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch (OperationCanceledException) when (lifetime.IsEnded) { }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private async void OnDestroy() => await ReleaseAsync();

        private async Task ReleaseAsync()
        {
            try
            {
                await lifetime.DisposeAsync();
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
