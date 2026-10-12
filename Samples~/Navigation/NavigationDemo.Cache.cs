using System;
using System.Threading.Tasks;
using MUI.Pages;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>展示关闭后仅复用 View，重新创建模型、Presenter 与导航身份。</summary>
        private async Task DemonstrateCacheAsync(PageManager pages, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var route = new PageDefinition<PageViewModel, PageArgs, int>(
                "demo.cache", resource, () => new PageViewModel(),
                _ => new CachedPagePresenter(), binding,
                host.ResolvePolicy(overrides: new PagePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }),
                estimatedRetainedBytes: 1024 * 1024);
            var first = await pages.OpenAsync(route, new PageArgs("首次打开", 10));
            if (!first.IsSuccess || !pages.TryGetViewModel<PageViewModel>(first.Handle.Identity, out var firstModel))
            {
                throw new InvalidOperationException("Cache walkthrough initial open failed.", first.Error);
            }

            var firstView = FindFrontView();
            await pages.CloseAsync(first.Handle);
            await pages.WaitForCleanupAsync(first.Handle.Identity);
            var firstResult = await first.Handle.WaitForResultAsync();
            Debug.Log($"MUI 缓存关闭：结果={firstResult.Status}，缓存数量={pages.CachedViewCount}");

            // 缓存命中与首次打开使用同一个异步入口，本地完成不额外等待一帧。
            var second = await pages.OpenAsync(route, new PageArgs("缓存重新打开", 20));
            if (!second.IsSuccess || !pages.TryGetViewModel<PageViewModel>(second.Handle.Identity, out var secondModel))
            {
                throw new InvalidOperationException("Cache walkthrough reopen failed.", second.Error);
            }

            Debug.Log($"MUI 缓存复开：同一模型={ReferenceEquals(firstModel, secondModel)}，" +
                $"同一视图={ReferenceEquals(firstView, FindFrontView())}，" +
                $"新句柄={first.Handle.Identity != second.Handle.Identity}，参数={secondModel.Selection}");
            var staleClose = await pages.CloseAsync(first.Handle);
            await pages.WaitForCleanupAsync(first.Handle.Identity);
            Debug.Log($"MUI 旧句柄关闭：{staleClose.Status}，新激活={pages.GetState(second.Handle.Identity)}");

            await pages.CloseAsync(second.Handle);
            await pages.ClearCacheAsync();
            Debug.Log($"MUI 缓存清空：剩余={pages.CachedViewCount}；此时 View 凭证应已实际归还");
            await DemonstrateCacheInvalidationAsync(pages, route);
            await DemonstrateCachePoliciesAsync(pages, resource, binding);
        }

        /// <summary>旧页面失去缓存资格后才关闭，展示仅清目录无法覆盖的迟到入缓存场景。</summary>
        private async Task DemonstrateCacheInvalidationAsync(PageManager pages,
            PageDefinition<PageViewModel, PageArgs, int> route)
        {
            var before = await pages.OpenAsync(route, new PageArgs("失效前打开", 30));
            if (!before.IsSuccess || !pages.TryGetViewModel<PageViewModel>(before.Handle.Identity, out var oldModel))
            {
                throw new InvalidOperationException("Cache invalidation walkthrough initial open failed.", before.Error);
            }

            await pages.InvalidateCacheAsync();
            Debug.Log($"MUI 缓存失效：活动页状态={pages.GetState(before.Handle.Identity)}");
            await pages.CloseAsync(before.Handle);
            Debug.Log($"MUI 旧页面关闭：缓存数量={pages.CachedViewCount}，预留字节={pages.ReservedCacheEstimatedBytes}");

            var after = await pages.OpenAsync(route, new PageArgs("失效后打开", 40));
            if (!after.IsSuccess || !pages.TryGetViewModel<PageViewModel>(after.Handle.Identity, out var newModel))
            {
                throw new InvalidOperationException("Cache invalidation walkthrough reopen failed.", after.Error);
            }

            Debug.Log($"MUI 新代际打开：重新创建模型={!ReferenceEquals(oldModel, newModel)}");
            await pages.CloseAsync(after.Handle);
            Debug.Log($"MUI 新页面关闭：缓存数量={pages.CachedViewCount}");
            await pages.ClearCacheAsync();
        }

        /// <summary>演示停用过期与两项容量下的淘汰；延时属于示例业务流程。</summary>
        private async Task DemonstrateCachePoliciesAsync(PageManager pages, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            PageDefinition<PageViewModel, PageArgs, int> CreateDefinition(string key, PagePolicy policy,
                long? estimate = 1024 * 1024) =>
                new PageDefinition<PageViewModel, PageArgs, int>(key, resource, () => new PageViewModel(),
                    _ => new CachedPagePresenter(), binding, policy, estimatedRetainedBytes: estimate);

            async Task<IView> OpenAndCloseAsync(PageDefinition<PageViewModel, PageArgs, int> route)
            {
                var opened = await pages.OpenAsync(route, new PageArgs(route.Key, 1));
                if (!opened.IsSuccess)
                {
                    throw new InvalidOperationException("Cache policy walkthrough open failed.", opened.Error);
                }

                var view = FindFrontView();
                await pages.CloseAsync(opened.Handle);
                await pages.WaitForCleanupAsync(opened.Handle.Identity);
                return view;
            }

            var timed = CreateDefinition("demo.cache.timed", host.ResolvePolicy(overrides: new PagePolicyOverrides
            {
                CacheMode = ViewCacheMode.Timed,
                CacheDuration = TimeSpan.FromMilliseconds(100)
            }));
            var beforeExpiry = await OpenAndCloseAsync(timed);
            await Task.Delay(200, cancellation.Token);
            var afterExpiry = await OpenAndCloseAsync(timed);
            Debug.Log($"MUI 缓存过期：重新创建视图={!ReferenceEquals(beforeExpiry, afterExpiry)}");
            pages.RefreshCache();
            await pages.ClearCacheAsync();

            var first = CreateDefinition("demo.cache.lru.first", host.ResolvePolicy(overrides: new PagePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }));
            var second = CreateDefinition("demo.cache.lru.second", host.ResolvePolicy(overrides: new PagePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }));
            var third = CreateDefinition("demo.cache.lru.third", host.ResolvePolicy(overrides: new PagePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }));
            var firstView = await OpenAndCloseAsync(first);
            var secondView = await OpenAndCloseAsync(second);
            await OpenAndCloseAsync(first);
            await OpenAndCloseAsync(third);
            var firstAgain = await OpenAndCloseAsync(first);
            var secondAgain = await OpenAndCloseAsync(second);
            Debug.Log($"MUI 缓存淘汰：最近使用视图保留={ReferenceEquals(firstView, firstAgain)}，" +
                $"较旧视图重新创建={!ReferenceEquals(secondView, secondAgain)}，" +
                $"目录={pages.CachedViewCount}，待释放={pages.RetiringCachedViewCount}");
            await pages.ClearCacheAsync();

            // 数值仅用于展示预算协议，不是本示例 Prefab 的实测内存。
            var larger = CreateDefinition("demo.cache.budget.large",
                host.ResolvePolicy(overrides: new PagePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }), 1536 * 1024);
            await OpenAndCloseAsync(first);
            await OpenAndCloseAsync(larger);
            Debug.Log($"MUI 字节预算淘汰：目录={pages.CachedViewCount}，" +
                $"预留字节={pages.ReservedCacheEstimatedBytes}，失败字节={pages.FailedCacheEstimatedBytes}");
            var unknown = CreateDefinition("demo.cache.budget.unknown",
                host.ResolvePolicy(overrides: new PagePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }), null);
            var oversized = CreateDefinition("demo.cache.budget.oversized",
                host.ResolvePolicy(overrides: new PagePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }), 3 * 1024 * 1024);
            await OpenAndCloseAsync(unknown);
            await OpenAndCloseAsync(oversized);
            Debug.Log($"MUI 未知或过大项不缓存：目录={pages.CachedViewCount}，预留字节={pages.ReservedCacheEstimatedBytes}");
            await pages.ClearCacheAsync();
            Debug.Log($"MUI 预算清空：预留字节={pages.ReservedCacheEstimatedBytes}，失败字节={pages.FailedCacheEstimatedBytes}");
        }

        /// <summary>每次打开使用新实例，业务资源随本次关闭清理。</summary>
        private sealed class CachedPagePresenter : PagePresenter
        {
            protected override void OnOpen(PageArgs args)
            {
                base.OnOpen(args);
                ViewModel.Item = new ThingItemViewModel { Label = "本次参数：" + args.Selection };
                ViewModel.DynamicSource = null;
                ViewModel.DynamicItem = null;
                ViewModel.SelectedItemKey = null;
                ViewModel.VirtualItems = PageViewModel.CreateVirtualItems(5);
            }
        }
    }
}
