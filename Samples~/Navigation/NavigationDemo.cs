using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Navigation;
using MUI.Resources;
using MUI.Samples.ResourceIntegration;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo : MonoBehaviour
    {
        [SerializeField] private bool automaticWalkthrough;
        [SerializeField] private bool showEnterTransitionPreview;
        [SerializeField] private bool showMeasuredListPreview = false;
        [SerializeField] private bool showCacheWalkthrough = false;
        [SerializeField] private bool showArgsUpdateWalkthrough = false;
        [SerializeField] private bool showRebindWalkthrough = false;
        [SerializeField] private bool showCloseTimeoutWalkthrough = false;
        [SerializeField] private bool showSynchronousGuardWalkthrough = false;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private UIHost host;

        public bool ShowEnterTransitionPreview
        {
            get => showEnterTransitionPreview; set => showEnterTransitionPreview = value;
        }

        public bool AutomaticWalkthrough
        {
            get => automaticWalkthrough; set => automaticWalkthrough = value;
        }

        private void Start()
        {
            _ = RunAsync();
        }

        private async Task RunAsync()
        {
            try
            {
                DemonstrateCollections();
                canvasObject = new GameObject("MUI Navigation Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(960, 540);
                if (EventSystem.current == null)
                {
                    eventSystemObject = new GameObject("MUI Navigation EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                }

                DemonstrateSafeArea(canvasObject.GetComponent<Canvas>());
                var template = BuildTemplate(canvasObject.transform);
                var resource = new ViewResource("NavigationView");
                var loader = new DemoPrefabLoader(template);
                // 演示值是保守预算输入，不代表 Prefab 的实测内存。
                var loadedProvider = new LoadedPrefabViewProvider(canvasObject.transform, loader,
                    identity => identity.Key + "@" + identity.Version);
                try
                {
                    Debug.Log("MUI Preload before: " + loadedProvider.GetSyncAvailability(resource));
                    var preload = await loadedProvider.PreloadAsync(resource, cancellation.Token);
                    Debug.Log($"MUI Preload ready: {loadedProvider.GetSyncAvailability(resource)}; loads={loader.Loads}");
                    var created = loadedProvider.Create(resource);
                    var native = (View)created.View;
                    await preload.DisposeAsync();
                    Debug.Log($"MUI Preload released with view alive: releases={loader.Releases}; nativeAlive={native != null}");
                    await created.DisposeAsync();
                    Debug.Log($"MUI Preload view released: releases={loader.Releases}; nativeDestroyed={native == null}; sync={loadedProvider.GetSyncAvailability(resource)}");
                    using (var cancelled = new CancellationTokenSource())
                    {
                        var late = loadedProvider.PreloadAsync(resource, cancelled.Token);
                        cancelled.Cancel();
                        try
                        {
                            await late;
                        }
                        catch (OperationCanceledException) { Debug.Log($"MUI Preload late cancelled: loads={loader.Loads}; releases={loader.Releases}"); }
                    }
                    var preloadNavigator = new Navigator(loadedProvider, preloadCapacity: 1);
                    var preloadRoute = new Route<PageViewModel, Unit, Unit>("demo.preload", resource,
                        () => throw new InvalidOperationException("Preloading must not create a model."),
                        bindingFactory: PageViewModelBindingFactory.Create);
                    var extraRoute = new Route<PageViewModel, Unit, Unit>("demo.preload.extra", new ViewResource("Other"),
                        () => new PageViewModel(), bindingFactory: PageViewModelBindingFactory.Create);
                    try
                    {
                        var warm = preloadNavigator.PreloadAsync(preloadRoute).AsTask();
                        var duplicateWarm = await preloadNavigator.PreloadAsync(preloadRoute, new CancellationToken(true));
                        var full = await preloadNavigator.PreloadAsync(extraRoute);
                        Debug.Log($"MUI Navigator preload: duplicate={duplicateWarm.Status}; full={full.Status}; result={(await warm).Status}; reservations={preloadNavigator.PreloadReservationCount}");
                        await preloadNavigator.ClearPreloadsAsync();
                        Debug.Log($"MUI Navigator preload cleared: reservations={preloadNavigator.PreloadReservationCount}; sync={loadedProvider.GetSyncAvailability(resource)}");
                        var staleWarm = preloadNavigator.PreloadAsync(preloadRoute).AsTask();
                        var clearing = preloadNavigator.ClearPreloadsAsync().AsTask();
                        var retiringFull = await preloadNavigator.PreloadAsync(preloadRoute);
                        await clearing;
                        Debug.Log($"MUI Navigator preload late clear: old={(await staleWarm).Status}; whileRetiring={retiringFull.Status}; reservations={preloadNavigator.PreloadReservationCount}");
                        await preloadNavigator.PreloadAsync(preloadRoute);
                        loadedProvider.Invalidate();
                        var refreshedWarm = await preloadNavigator.PreloadAsync(preloadRoute);
                        Debug.Log($"MUI 资源代际变更：预加载={refreshedWarm.Status}，加载次数={loader.Loads}，释放次数={loader.Releases}");
                        await preloadNavigator.ClearPreloadsAsync();
                        var staleVersion = preloadNavigator.PreloadAsync(preloadRoute).AsTask();
                        loadedProvider.Invalidate();
                        Debug.Log($"MUI 旧代际迟到加载：结果={(await staleVersion).Status}，加载次数={loader.Loads}，释放次数={loader.Releases}");
                        var memoryWarm = preloadNavigator.PreloadAsync(preloadRoute).AsTask();
                        var memoryTrim = preloadNavigator.ClearInactiveContentAsync().AsTask();
                        var whileTrimming = await preloadNavigator.PreloadAsync(preloadRoute);
                        await memoryTrim;
                        Debug.Log($"MUI 低内存预加载回收：旧请求={(await memoryWarm).Status}，" +
                            $"回收中新请求={whileTrimming.Status}，剩余额度={preloadNavigator.PreloadReservationCount}");
                    }
                    finally { await preloadNavigator.ShutdownAsync(); }
                    Debug.Log($"MUI Navigator preload shutdown: reservations={preloadNavigator.PreloadReservationCount}; loads={loader.Loads}; releases={loader.Releases}");
                }
                finally { await loadedProvider.DisposeAsync(); }
                var provider = new PrefabViewProvider(canvasObject.transform,
                    new[]
                    {
                        new KeyValuePair<ViewResource, GameObject>(resource, template),
                        new KeyValuePair<ViewResource, GameObject>(new ViewResource("ThingItem"), template.transform.Find("ThingItem").gameObject)
                    });
                var dynamicProvider = new DelayedItemProvider(provider);
                BindingContext<PageViewModel> BindPage(IView view, PageViewModel model)
                {
                    var page = (View)view;
                    page.GetElement<DynamicViewElement>("DynamicItem").Configure(dynamicProvider);
                    ConfigureBackInput(page);
                    return PageViewModelBindingFactory.Create(view, model);
                }
                PageViewModelBindingFactory.Register();
                ThingItemViewModelBindingFactory.Register();
                host = canvasObject.AddComponent<UIHost>();
                host.Initialize(provider, ownsProvider: true, cacheCapacity: showCacheWalkthrough ? 2 : 16,
                    maxCachedEstimatedBytes: showCacheWalkthrough ? (long?)(2 * 1024 * 1024) : null,
                    cleanupCapacity: showCloseTimeoutWalkthrough ? 1 : 16);
                var navigator = host.Navigator;
                navigator.LifecycleChanged += LogLifecycle;
                if (showSynchronousGuardWalkthrough)
                {
                    await DemonstrateSynchronousGuardAsync(navigator, resource, BindPage);
                }
                var synchronous = new Route<PageViewModel, PageArgs, int>("demo.sync", resource,
                    () => new PageViewModel(), model => new PagePresenter(), BindPage,
                    preparation: PreparationMode.Synchronous);
                var asynchronous = new Route<PageViewModel, PageArgs, int>("demo.async", resource,
                    () => new PageViewModel(), model => new AsyncPagePresenter(navigator, synchronous), BindPage);
                if (showEnterTransitionPreview)
                {
                    await ShowEnterTransitionPreviewAsync(navigator, resource, BindPage);
                }

                if (showCacheWalkthrough)
                {
                    await DemonstrateCacheAsync(navigator, resource, BindPage);
                }

                if (showArgsUpdateWalkthrough)
                {
                    await DemonstrateArgsUpdateAsync(navigator, resource, BindPage);
                }

                if (showRebindWalkthrough)
                {
                    await DemonstrateRebindAsync(navigator, resource, BindPage);
                }

                if (showCloseTimeoutWalkthrough)
                {
                    await DemonstrateCloseTimeoutAsync(navigator, resource, BindPage);
                }

                var first = navigator.Open(synchronous, new PageArgs("Synchronous page", 10));
                Debug.Log("MUI Navigation sync open: " + first.Status);
                if (!first.IsSuccess)
                {
                    throw new InvalidOperationException("Synchronous demo page could not open.", first.Error);
                }

                var firstView = FindFrontView();
                if (showMeasuredListPreview)
                {
                    if (!navigator.TryGetViewModel<PageViewModel>(first.Handle.Identity, out var measuredModel))
                    {
                        throw new InvalidOperationException("Measured list preview model is unavailable.");
                    }

                    await DemonstrateMeasuredListAsync(firstView, measuredModel);
                    return;
                }
                await DemonstrateCoverageAsync(navigator, resource, BindPage, first.Handle.Identity, firstView);
                await DemonstrateBackAsync(navigator, resource, BindPage, first.Handle.Identity);
                await DemonstrateInputAsync(navigator, resource, BindPage);
                await DemonstrateFocusAsync(navigator, resource, BindPage, firstView, first.Handle.Identity);
                await DemonstrateTicksAsync(navigator, resource, BindPage);
                await DemonstrateChildTicksAsync(firstView);
                var nested = firstView.GetElement<NestedViewElement>("NestedItem");
                if (!navigator.TryGetViewModel<PageViewModel>(first.Handle.Identity, out var pageModel))
                {
                    throw new InvalidOperationException("Demo page model is unavailable.");
                }

                Debug.Log("MUI Nested initial: " + ((ThingItemViewModel)nested.DisplayedViewModel).Label);
                await DemonstrateVirtualListAsync(firstView, pageModel);
                var originalItem = pageModel.Item;
                pageModel.Item = new ThingItemViewModel { Label = "Stone × 8" };
                await nested.PendingChange;
                originalItem.Label = "Detached old item";
                Debug.Log("MUI Nested replaced: " + ((ThingItemViewModel)nested.DisplayedViewModel).Label);
                pageModel.Item = null;
                await nested.PendingChange;
                Debug.Log("MUI Nested null clears: " + (nested.DisplayedViewModel == null));
                pageModel.Item = new ThingItemViewModel { Label = "Stone × 12" };
                await nested.PendingChange;
                Debug.Log("MUI Nested rebound: " + ((ThingItemViewModel)nested.DisplayedViewModel).Label);
                var dynamic = firstView.GetElement<DynamicViewElement>("DynamicItem");
                Debug.Log("MUI Dynamic initial: " + ((ThingItemViewModel)dynamic.DisplayedViewModel).Label);
                var originalDynamicView = dynamic.GetComponentInChildren<View>();
                pageModel.DynamicItem = new ThingItemViewModel { Label = "Iron × 6" };
                var modelChange = await dynamic.PendingChange;
                var reboundDynamicView = dynamic.GetComponentInChildren<View>();
                Debug.Log($"MUI Dynamic model change: {modelChange.Status}; reused={originalDynamicView != null && originalDynamicView == reboundDynamicView}");
                pageModel.DynamicSource = "SlowThingItem";
                var slowItem = dynamic.PendingChange;
                pageModel.DynamicSource = "ThingItem";
                var latestItem = await dynamic.PendingChange;
                Debug.Log($"MUI Dynamic latest request: old={(await slowItem).Status}, latest={latestItem.Status}, displayed={dynamic.DisplayedSource}");
                pageModel.DynamicItem = null;
                await dynamic.PendingChange;
                Debug.Log($"MUI Dynamic null model: instance={dynamic.HasInstance}, unbound={dynamic.DisplayedViewModel == null}");
                pageModel.DynamicItem = new ThingItemViewModel { Label = "Iron × 7" };
                await dynamic.PendingChange;
                Debug.Log("MUI Dynamic rebound: " + ((ThingItemViewModel)dynamic.DisplayedViewModel).Label);
                pageModel.DynamicSource = null;
                await dynamic.PendingChange;
                Debug.Log("MUI Dynamic source cleared: instance=" + dynamic.HasInstance);
                var itemView = firstView.transform.Find("ThingItem").GetComponent<View>();
                var itemResource = new ViewResource("ThingItem");
                var itemProvider = new BorrowedViewProvider(itemResource, itemView);
                var itemModel = new ThingItemViewModel();
                var itemTemplate = new ChildViewTemplate<ThingItemViewModel, string>(itemResource,
                    () => new ThingItemViewModel(), ThingItemViewModelBindingFactory.Create,
                    supportsSynchronousPreparation: true);
                var item = firstView.ChildViews.Prepare(itemTemplate, itemProvider, "Wood", itemModel);
                Debug.Log($"MUI ChildView prepared: {item.State}; alpha={itemView.GetComponent<CanvasGroup>().alpha}");
                item.Commit();
                Debug.Log($"MUI ChildView committed: {item.State}; alpha={itemView.GetComponent<CanvasGroup>().alpha}");
                item.SetLocalState(false, false);
                firstView.Visible = false;
                firstView.Visible = true;
                Debug.Log($"MUI ChildView local hidden after parent restore: alpha={itemView.GetComponent<CanvasGroup>().alpha}");
                item.SetLocalState(true, true);
                await item.DisposeAsync();
                Debug.Log($"MUI ChildView borrowed release: {item.State}; nodeAlive={itemView != null}");
                ThingItemPresenter itemPresenter = null;
                var asyncItemTemplate = new ChildViewTemplate<ThingItemViewModel, string>(itemResource,
                    () => new ThingItemViewModel(), ThingItemViewModelBindingFactory.Create,
                    _ => itemPresenter = new ThingItemPresenter());
                var asyncItem = await firstView.ChildViews.PrepareAsync(asyncItemTemplate, itemProvider,
                    "Wood", itemModel, cancellation.Token);
                asyncItem.Commit();
                Debug.Log($"MUI ChildView async item: {itemModel.Label}; state={asyncItem.State}");
                if (showArgsUpdateWalkthrough)
                {
                    await DemonstrateChildArgsUpdateAsync(firstView.ChildViews, asyncItem, itemPresenter);
                }

                if (showRebindWalkthrough)
                {
                    await DemonstrateChildRebindAsync(asyncItem, itemPresenter);
                }

                var refused = navigator.Open(asynchronous, new PageArgs("Async page", 42));
                Debug.Log("MUI Navigation sync async-only route: " + refused.Rejection);
                var second = await navigator.OpenAsync(asynchronous, new PageArgs("Asynchronous page", 42), cancellation.Token);
                Debug.Log("MUI Navigation async open: " + second.Status);
                if (!second.IsSuccess)
                {
                    throw new InvalidOperationException("Asynchronous demo page could not open.", second.Error);
                }

                if (!automaticWalkthrough)
                {
                    return;
                }

                await Task.Delay(150, cancellation.Token);
                FindFrontView().GetElement<ButtonElement>("Confirm").GetComponent<Button>().onClick.Invoke();
                var selection = await second.Handle.WaitForResultAsync(cancellation.Token);
                Debug.Log($"MUI Navigation result: {selection.Status}, value={selection.Value}, cleanup={selection.Cleanup}");
                var duplicate = await navigator.CloseAsync(second.Handle);
                Debug.Log("MUI Navigation repeated close: " + duplicate.Status);
                await navigator.BackAsync(cancellation.Token);
                var dismissed = await first.Handle.WaitForResultAsync(cancellation.Token);
                Debug.Log($"MUI Navigation back: {dismissed.Status}/{dismissed.Reason}; history={navigator.History.Count}");
                Debug.Log($"MUI ChildView parent closed: {asyncItem.State}");
                using (var cancelledOpen = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
                {
                    cancelledOpen.CancelAfter(20);
                    var cancelled = await navigator.OpenAsync(asynchronous, new PageArgs("Cancelled preparation", 0), cancelledOpen.Token);
                    Debug.Log($"MUI Navigation cancelled prepare: {cancelled.Status}; history={navigator.History.Count}");
                }
                var closingPage = await navigator.OpenAsync(asynchronous, new PageArgs("Cancelled close waiter", 0), cancellation.Token);
                if (!closingPage.IsSuccess)
                {
                    throw new InvalidOperationException("Close waiter demo could not open.", closingPage.Error);
                }

                var cancelledWait = await navigator.CloseAsync(closingPage.Handle, new CancellationToken(true));
                Debug.Log($"MUI Navigation cancelled close wait: {cancelledWait.Status}; state={navigator.GetState(closingPage.Handle.Identity)}");
                var stillClosed = await closingPage.Handle.WaitForResultAsync(cancellation.Token);
                Debug.Log($"MUI Navigation close continued: {stillClosed.Status}; cleanup={stillClosed.Cleanup}");
                await host.ShutdownAsync();
                Debug.Log("MUI Navigation shutdown complete");
            }
            catch (OperationCanceledException)
            {
                // 演示被关闭时取消在途流程，由退出逻辑继续清理。
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private View FindFrontView()
        {
            View found = null;
            foreach (var view in canvasObject.GetComponentsInChildren<View>())
            {
                if (view.transform.parent == canvasObject.transform && view.IsAlive &&
                    (found == null || view.transform.GetSiblingIndex() > found.transform.GetSiblingIndex()))
                {
                    found = view;
                }
            }

            if (found == null)
            {
                throw new InvalidOperationException("No active demo View.");
            }

            return found;
        }

        private GameObject BuildTemplate(Transform parent)
        {
            var root = Node("NavigationView", parent, new Vector2(920, 430), Vector2.zero);
            root.gameObject.SetActive(false);
            root.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.17f, 0.23f);
            var title = AddText("Title", "Preparing", root, new Vector2(460, 70), new Vector2(0, 160));
            title.gameObject.AddComponent<TextElement>();
            var item = Node("ThingItem", root, new Vector2(360, 70), new Vector2(0, 70));
            item.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.24f, 0.3f);
            AddText("ItemLabel", "Wood", item, new Vector2(330, 60), Vector2.zero).gameObject.AddComponent<TextElement>();
            item.gameObject.AddComponent<View>();
            var nested = Node("NestedItem", root, new Vector2(360, 70), new Vector2(0, -20));
            var nestedView = Node("NestedItemView", nested, new Vector2(360, 70), Vector2.zero);
            nestedView.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.28f, 0.32f);
            AddText("ItemLabel", "Stone", nestedView, new Vector2(330, 60), Vector2.zero).gameObject.AddComponent<TextElement>();
            nestedView.gameObject.AddComponent<View>();
            nested.gameObject.AddComponent<NestedViewElement>();
            Node("DynamicItem", root, new Vector2(360, 70), new Vector2(0, -95)).gameObject.AddComponent<DynamicViewElement>();
            AddButton("Confirm", "Confirm", root, new Vector2(-110, -155));
            AddButton("Close", "Close", root, new Vector2(110, -155));
            BuildVirtualList(root, showMeasuredListPreview);
            root.gameObject.AddComponent<View>();
            return root.gameObject;
        }

        private static RectTransform Node(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = new Vector2(0.5f, 0.5f);
            node.sizeDelta = size;
            node.anchoredPosition = position;
            return node;
        }

        private static Text AddText(string name, string caption, Transform parent, Vector2 size, Vector2 position)
        {
            var text = Node(name, parent, size, position).gameObject.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = caption;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static void AddButton(string name, string caption, Transform parent, Vector2 position)
        {
            var node = Node(name, parent, new Vector2(180, 48), position);
            var image = node.gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.6f);
            node.gameObject.AddComponent<Button>().targetGraphic = image;
            node.gameObject.AddComponent<ButtonElement>();
            AddText("Label", caption, node, new Vector2(170, 45), Vector2.zero);
        }

        private void OnDestroy()
        {
            cancellation.Cancel();
            cancellation.Dispose();
            _ = CleanupAsync();
        }

        private async Task CleanupAsync()
        {
            try
            {
                if (host != null)
                {
                    await host.ShutdownAsync();
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                if (canvasObject != null)
                {
                    Destroy(canvasObject);
                }

                if (eventSystemObject != null)
                {
                    Destroy(eventSystemObject);
                }
            }
        }
    }
}
