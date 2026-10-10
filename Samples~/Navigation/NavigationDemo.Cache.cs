using System;
using System.Threading.Tasks;
using MUI.Navigation;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        /// <summary>展示关闭后仅复用 View，重新创建模型、Presenter 与导航身份。</summary>
        private async Task DemonstrateCacheAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            var route = new Route<PageViewModel, PageArgs, int>(
                "demo.cache", resource, () => new PageViewModel(),
                _ => new CachedPagePresenter(), binding,
                host.ResolvePolicy(overrides: new RoutePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }),
                estimatedRetainedBytes: 1024 * 1024);
            var first = await navigator.OpenAsync(route, new PageArgs("首次打开", 10));
            if (!first.IsSuccess || !navigator.TryGetViewModel<PageViewModel>(first.Handle.Identity, out var firstModel))
            {
                throw new InvalidOperationException("Cache walkthrough initial open failed.", first.Error);
            }

            var firstView = FindFrontView();
            await navigator.CloseAsync(first.Handle);
            await navigator.WaitForCleanupAsync(first.Handle.Identity);
            var firstResult = await first.Handle.WaitForResultAsync();
            Debug.Log($"MUI 缓存关闭：结果={firstResult.Status}，缓存数量={navigator.CachedViewCount}");

            // 缓存命中与首次打开使用同一个异步入口，本地完成不额外等待一帧。
            var second = await navigator.OpenAsync(route, new PageArgs("缓存重新打开", 20));
            if (!second.IsSuccess || !navigator.TryGetViewModel<PageViewModel>(second.Handle.Identity, out var secondModel))
            {
                throw new InvalidOperationException("Cache walkthrough reopen failed.", second.Error);
            }

            Debug.Log($"MUI 缓存复开：同一模型={ReferenceEquals(firstModel, secondModel)}，" +
                $"同一视图={ReferenceEquals(firstView, FindFrontView())}，" +
                $"新句柄={first.Handle.Identity != second.Handle.Identity}，参数={secondModel.Selection}");
            var staleClose = await navigator.CloseAsync(first.Handle);
            await navigator.WaitForCleanupAsync(first.Handle.Identity);
            Debug.Log($"MUI 旧句柄关闭：{staleClose.Status}，新激活={navigator.GetState(second.Handle.Identity)}");

            await navigator.CloseAsync(second.Handle);
            await navigator.ClearCacheAsync();
            Debug.Log($"MUI 缓存清空：剩余={navigator.CachedViewCount}；此时 View 凭证应已实际归还");
            await DemonstrateCacheInvalidationAsync(navigator, route);
            await DemonstrateCachePoliciesAsync(navigator, resource, binding);
        }

        /// <summary>旧页面失去缓存资格后才关闭，展示仅清目录无法覆盖的迟到入缓存场景。</summary>
        private async Task DemonstrateCacheInvalidationAsync(Navigator navigator,
            Route<PageViewModel, PageArgs, int> route)
        {
            var before = await navigator.OpenAsync(route, new PageArgs("失效前打开", 30));
            if (!before.IsSuccess || !navigator.TryGetViewModel<PageViewModel>(before.Handle.Identity, out var oldModel))
            {
                throw new InvalidOperationException("Cache invalidation walkthrough initial open failed.", before.Error);
            }

            await navigator.InvalidateCacheAsync();
            Debug.Log($"MUI 缓存失效：活动页状态={navigator.GetState(before.Handle.Identity)}");
            await navigator.CloseAsync(before.Handle);
            Debug.Log($"MUI 旧页面关闭：缓存数量={navigator.CachedViewCount}，预留字节={navigator.ReservedCacheEstimatedBytes}");

            var after = await navigator.OpenAsync(route, new PageArgs("失效后打开", 40));
            if (!after.IsSuccess || !navigator.TryGetViewModel<PageViewModel>(after.Handle.Identity, out var newModel))
            {
                throw new InvalidOperationException("Cache invalidation walkthrough reopen failed.", after.Error);
            }

            Debug.Log($"MUI 新代际打开：重新创建模型={!ReferenceEquals(oldModel, newModel)}");
            await navigator.CloseAsync(after.Handle);
            Debug.Log($"MUI 新页面关闭：缓存数量={navigator.CachedViewCount}");
            await navigator.ClearCacheAsync();
        }

        /// <summary>演示停用过期与两项容量下的淘汰；延时属于示例业务流程。</summary>
        private async Task DemonstrateCachePoliciesAsync(Navigator navigator, ViewResource resource,
            Func<IView, PageViewModel, BindingContext<PageViewModel>> binding)
        {
            Route<PageViewModel, PageArgs, int> CreateRoute(string key, RoutePolicy policy,
                long? estimate = 1024 * 1024) =>
                new Route<PageViewModel, PageArgs, int>(key, resource, () => new PageViewModel(),
                    _ => new CachedPagePresenter(), binding, policy, estimatedRetainedBytes: estimate);

            async Task<IView> OpenAndCloseAsync(Route<PageViewModel, PageArgs, int> route)
            {
                var opened = await navigator.OpenAsync(route, new PageArgs(route.Key, 1));
                if (!opened.IsSuccess)
                {
                    throw new InvalidOperationException("Cache policy walkthrough open failed.", opened.Error);
                }

                var view = FindFrontView();
                await navigator.CloseAsync(opened.Handle);
                await navigator.WaitForCleanupAsync(opened.Handle.Identity);
                return view;
            }

            var timed = CreateRoute("demo.cache.timed", host.ResolvePolicy(overrides: new RoutePolicyOverrides
            {
                CacheMode = ViewCacheMode.Timed,
                CacheDuration = TimeSpan.FromMilliseconds(100)
            }));
            var beforeExpiry = await OpenAndCloseAsync(timed);
            await Task.Delay(200, cancellation.Token);
            var afterExpiry = await OpenAndCloseAsync(timed);
            Debug.Log($"MUI 缓存过期：重新创建视图={!ReferenceEquals(beforeExpiry, afterExpiry)}");
            navigator.RefreshCache();
            await navigator.ClearCacheAsync();

            var first = CreateRoute("demo.cache.lru.first", host.ResolvePolicy(overrides: new RoutePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }));
            var second = CreateRoute("demo.cache.lru.second", host.ResolvePolicy(overrides: new RoutePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }));
            var third = CreateRoute("demo.cache.lru.third", host.ResolvePolicy(overrides: new RoutePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }));
            var firstView = await OpenAndCloseAsync(first);
            var secondView = await OpenAndCloseAsync(second);
            await OpenAndCloseAsync(first);
            await OpenAndCloseAsync(third);
            var firstAgain = await OpenAndCloseAsync(first);
            var secondAgain = await OpenAndCloseAsync(second);
            Debug.Log($"MUI 缓存淘汰：最近使用视图保留={ReferenceEquals(firstView, firstAgain)}，" +
                $"较旧视图重新创建={!ReferenceEquals(secondView, secondAgain)}，" +
                $"目录={navigator.CachedViewCount}，待释放={navigator.RetiringCachedViewCount}");
            await navigator.ClearCacheAsync();

            // 数值仅用于展示预算协议，不是本示例 Prefab 的实测内存。
            var larger = CreateRoute("demo.cache.budget.large",
                host.ResolvePolicy(overrides: new RoutePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }), 1536 * 1024);
            await OpenAndCloseAsync(first);
            await OpenAndCloseAsync(larger);
            Debug.Log($"MUI 字节预算淘汰：目录={navigator.CachedViewCount}，" +
                $"预留字节={navigator.ReservedCacheEstimatedBytes}，失败字节={navigator.FailedCacheEstimatedBytes}");
            var unknown = CreateRoute("demo.cache.budget.unknown",
                host.ResolvePolicy(overrides: new RoutePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }), null);
            var oversized = CreateRoute("demo.cache.budget.oversized",
                host.ResolvePolicy(overrides: new RoutePolicyOverrides { CacheMode = ViewCacheMode.KeepAlive }), 3 * 1024 * 1024);
            await OpenAndCloseAsync(unknown);
            await OpenAndCloseAsync(oversized);
            Debug.Log($"MUI 未知或过大项不缓存：目录={navigator.CachedViewCount}，预留字节={navigator.ReservedCacheEstimatedBytes}");
            await navigator.ClearCacheAsync();
            Debug.Log($"MUI 预算清空：预留字节={navigator.ReservedCacheEstimatedBytes}，失败字节={navigator.FailedCacheEstimatedBytes}");
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
