using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;
using MUI.Resources;
using MUI.Tabs;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MUI.Samples.Tabs
{
    public sealed class TabsDemo : MonoBehaviour
    {
        [SerializeField] private bool automaticWalkthrough;
        [SerializeField] private bool blankWhileLoading;
        [SerializeField] private bool keepPreviousWhileLoading = false;
        [SerializeField] private TabFailureDisplay failureDisplay = TabFailureDisplay.ErrorPlaceholder;
        [SerializeField, Min(0)] private int cacheCapacity = 0;
        [SerializeField, Min(0)] private float cacheTimeToLiveSeconds = 0;
        [SerializeField, Min(0)] private float preparationTimeoutSeconds = 0;
        [SerializeField, Min(1)] private int maxQuarantinedPreparations = 2;
        [SerializeField, Min(0)] private int preparationDelayMilliseconds = 150;
        [SerializeField] private bool ignorePreparationCancellation = false;
        private readonly Lifetime lifetime = new Lifetime();
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private PrefabViewProvider provider;
        private VersionedTabProvider versionedProvider;
        private TabContentController controller;

        public bool AutomaticWalkthrough
        {
            get => automaticWalkthrough; set => automaticWalkthrough = value;
        }

        public bool BlankWhileLoading
        {
            get => blankWhileLoading; set => blankWhileLoading = value;
        }

        private void Start()
        {
            _ = RunAsync();
        }

        private async Task RunAsync()
        {
            try
            {
                if (cacheCapacity < 0 || preparationDelayMilliseconds < 0 || maxQuarantinedPreparations < 1 ||
                    preparationTimeoutSeconds < 0 || float.IsNaN(preparationTimeoutSeconds) || float.IsInfinity(preparationTimeoutSeconds) ||
                    cacheTimeToLiveSeconds < 0 || float.IsNaN(cacheTimeToLiveSeconds) || float.IsInfinity(cacheTimeToLiveSeconds) ||
                    cacheTimeToLiveSeconds > TimeSpan.MaxValue.TotalSeconds ||
                    (cacheTimeToLiveSeconds > 0 && cacheCapacity == 0))
                {
                    throw new InvalidOperationException("Invalid Tabs sample cache or preparation settings.");
                }

                // 定义捕获固定配置；运行中修改 Inspector 不应悄悄改变同一定义的缓存兼容性。
                var pageDelayMilliseconds = preparationDelayMilliseconds;
                var ignorePagePreparationCancellation = ignorePreparationCancellation;

                canvasObject = new GameObject("MUI Tabs Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(960, 540);
                if (EventSystem.current == null)
                {
                    eventSystemObject = new GameObject("MUI Tabs EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                }
                var root = Node("TabsView", canvasObject.transform, new Vector2(760, 440), Vector2.zero);
                root.gameObject.SetActive(false);
                root.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.15f, 0.20f);
                var region = Node("ContentRegion", root, new Vector2(700, 290), new Vector2(0, 50));
                Node("ContentHost", region, new Vector2(700, 290), Vector2.zero);
                AddText("LoadingOverlay", "Loading...", region, new Vector2(650, 60), Vector2.zero);
                AddText("ErrorOverlay", "Could not load.", region, new Vector2(650, 60), Vector2.zero);
                AddButton("Retry", "Retry", region, new Vector2(0, -80));
                var content = region.gameObject.AddComponent<AsyncContentElement>();
                var bar = Node("TabBar", root, new Vector2(700, 70), new Vector2(0, -155));
                AddTab("inventory", "Inventory", bar, -220);
                AddTab("quests", "Quests", bar, 0);
                AddTab("locked", "Locked", bar, 220);
                var tabBar = bar.gameObject.AddComponent<TabBarElement>();
                var generatedTemplate = Node("TabButtonTemplate", canvasObject.transform, new Vector2(180, 48), Vector2.zero);
                generatedTemplate.gameObject.SetActive(false);
                generatedTemplate.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.4f, 0.6f);
                var templateButton = generatedTemplate.gameObject.AddComponent<Button>();
                templateButton.targetGraphic = generatedTemplate.GetComponent<Image>();
                AddText("Label", "New tab", generatedTemplate, new Vector2(170, 45), Vector2.zero);
                Node("Selected", generatedTemplate, new Vector2(170, 4), new Vector2(0, -22)).gameObject.AddComponent<Image>().color = Color.cyan;
                tabBar.ConfigureButtonTemplate(templateButton);
                var view = root.gameObject.AddComponent<View>();
                view.Initialize();
                view.BeginChildActivation(lifetime);
                var prefab = Node("TabPage", canvasObject.transform, new Vector2(650, 220), Vector2.zero);
                prefab.gameObject.SetActive(false);
                prefab.gameObject.AddComponent<Image>().color = new Color(0.17f, 0.24f, 0.32f);
                AddText("Title", "Preparing", prefab, new Vector2(600, 80), Vector2.zero).gameObject.AddComponent<TextElement>();
                prefab.gameObject.AddComponent<View>();
                var resource = new ViewResource("TabPage");
                provider = new PrefabViewProvider(canvasObject.transform,
                    new[] { new KeyValuePair<ViewResource, GameObject>(resource, prefab.gameObject) });
                versionedProvider = new VersionedTabProvider(provider);
                var mounted = content.CreateProvider(versionedProvider);
                var template = new ChildViewTemplate<TabPageViewModel, TabPageArgs>(resource,
                    () => new TabPageViewModel(), TabPageViewModelBindingFactory.Create, _ => new TabPagePresenter());
                var questAttempts = 0;
                var allowLeave = true;
                var guardCalls = 0;
                var catalog = new[]
                {
                    new TabContentDefinition("inventory", "Inventory", async (scope, token) =>
                        await scope.PrepareAsync(template, mounted, new TabPageArgs("Inventory", delayMilliseconds: pageDelayMilliseconds, ignorePreparationCancellation: ignorePagePreparationCancellation), cancellationToken: token),
                        canLeaveAsync: async (leave, token) =>
                        {
                            ++guardCalls;
                            await Task.Delay(80, token);
                            return allowLeave;
                        }),
                    new TabContentDefinition("quests", "Quests", async (scope, token) =>
                        await scope.PrepareAsync(template, mounted, new TabPageArgs("Quests", ++questAttempts == 1, pageDelayMilliseconds, ignorePagePreparationCancellation), cancellationToken: token)),
                    new TabContentDefinition("locked", "Locked", async (scope, token) =>
                        await scope.PrepareAsync(template, mounted, new TabPageArgs("Locked", delayMilliseconds: pageDelayMilliseconds, ignorePreparationCancellation: ignorePagePreparationCancellation), cancellationToken: token), () => false)
                };
                controller = new TabContentController(view.ChildViews, catalog,
                    keepPreviousWhileLoading ? TabPendingDisplay.KeepPrevious :
                        blankWhileLoading ? TabPendingDisplay.Blank : TabPendingDisplay.LoadingPlaceholder,
                    TimeSpan.FromMilliseconds(40), failureDisplay,
                    cacheOptions: cacheCapacity == 0 ? null : new TabCacheOptions(TabContentRetention.CacheRecent, cacheCapacity,
                        timeToLive: cacheTimeToLiveSeconds == 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(cacheTimeToLiveSeconds)),
                    preparationOptions: new ChildViewPreparationOptions(
                        preparationTimeoutSeconds == 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(preparationTimeoutSeconds),
                        maxQuarantinedPreparations));
                content.Bind(controller);
                tabBar.Bind(controller);
                view.CommitChildActivation();
                view.SetHostState(true, true);
                root.gameObject.SetActive(true);
                var first = controller.SelectAsync("inventory").AsTask();
                if (!automaticWalkthrough)
                {
                    await first;
                    return;
                }
                var duplicate = await controller.SelectAsync("inventory", new CancellationToken(true));
                var disabled = await controller.SelectAsync("locked");
                Debug.Log($"MUI Tabs duplicate={duplicate.Status}; disabled={disabled.Status}/{disabled.Rejection}; selected={controller.ViewModel.Snapshot.SelectedTab}");
                await Task.Delay(70, lifetime.Token);
                Debug.Log($"MUI Tabs loading: phase={controller.ViewModel.Snapshot.Phase}, indicator={controller.ViewModel.Snapshot.LoadingIndicatorVisible}, barEnabled={bar.GetChild(1).GetComponent<Button>().interactable}");
                Debug.Log("MUI Tabs initial: " + (await first).Status);
                var failed = await controller.SelectAsync("quests");
                Debug.Log($"MUI Tabs failure: {failed.Status}; phase={controller.ViewModel.Snapshot.Phase}");
                var retried = await controller.RetryAsync();
                Debug.Log($"MUI Tabs retry: {retried.Status}; displayed={controller.ViewModel.Snapshot.DisplayedTab}");
                var old = controller.SelectAsync("inventory").AsTask();
                var latest = controller.SelectAsync("quests", forceReload: true).AsTask();
                Debug.Log($"MUI Tabs switch: old={(await old).Status}; latest={(await latest).Status}; displayed={controller.ViewModel.Snapshot.DisplayedTab}");
                var keptVersion = controller.ViewModel.Snapshot.RequestVersion;
                await controller.UpdateDefinitionsAsync(catalog);
                Debug.Log($"MUI Tabs unchanged catalog: keptVersion={keptVersion == controller.ViewModel.Snapshot.RequestVersion}");
                var invalid = await controller.UpdateDefinitionsAsync(new[] { catalog[0], catalog[0] });
                Debug.Log($"MUI Tabs invalid catalog: {invalid.Status}; count={controller.ViewModel.Items.Count}; displayed={controller.ViewModel.Snapshot.DisplayedTab}");
                var fallback = await controller.UpdateDefinitionsAsync(new[] { catalog[0], catalog[2] });
                Debug.Log($"MUI Tabs removed selected: {fallback.Status}; displayed={controller.ViewModel.Snapshot.DisplayedTab}; questsVisible={bar.Find("quests").gameObject.activeSelf}");
                var empty = await controller.UpdateDefinitionsAsync(Array.Empty<TabContentDefinition>());
                Debug.Log($"MUI Tabs empty catalog: {empty.Status}; phase={controller.ViewModel.Snapshot.Phase}");
                await controller.UpdateDefinitionsAsync(catalog, "quests");
                Debug.Log($"MUI Tabs restored catalog: displayed={controller.ViewModel.Snapshot.DisplayedTab}; questsVisible={bar.Find("quests").gameObject.activeSelf}");
                var dynamicDefinition = new TabContentDefinition("mail", "Mail", async (scope, token) =>
                    await scope.PrepareAsync(template, mounted, new TabPageArgs("Mail", delayMilliseconds: pageDelayMilliseconds, ignorePreparationCancellation: ignorePagePreparationCancellation), cancellationToken: token));
                await controller.UpdateDefinitionsAsync(new[] { dynamicDefinition });
                var generatedButton = bar.Find("mail").GetComponent<Button>();
                EventSystem.current.SetSelectedGameObject(generatedButton.gameObject);
                Debug.Log($"MUI Tabs generated: key={generatedButton.name}; label={generatedButton.transform.Find("Label").GetComponent<Text>().text}; active={generatedButton.gameObject.activeSelf}");
                await controller.UpdateDefinitionsAsync(catalog, "inventory");
                await Task.Yield();
                Debug.Log($"MUI Tabs generated removed: destroyed={generatedButton == null}; focus={EventSystem.current.currentSelectedGameObject.name}");
                allowLeave = false;
                var beforeGuard = controller.ViewModel.Snapshot.RequestVersion;
                var denied = await controller.SelectAsync("quests");
                Debug.Log($"MUI Tabs guard denied: {denied.Status}/{denied.Rejection}; displayed={controller.ViewModel.Snapshot.DisplayedTab}; keptVersion={beforeGuard == controller.ViewModel.Snapshot.RequestVersion}");
                allowLeave = true;
                var guarded = controller.SelectAsync("quests").AsTask();
                var guardedDuplicate = await controller.SelectAsync("quests", new CancellationToken(true));
                Debug.Log($"MUI Tabs guard duplicate: {guardedDuplicate.Status}; selection={(await guarded).Status}; calls={guardCalls}");
                await controller.SelectAsync("inventory");
                var supersededGuard = controller.SelectAsync("quests").AsTask();
                await Task.Delay(20, lifetime.Token);
                var latestGuard = controller.SelectAsync("inventory", forceReload: true).AsTask();
                Debug.Log($"MUI Tabs guard latest: old={(await supersededGuard).Status}; latest={(await latestGuard).Status}; displayed={controller.ViewModel.Snapshot.DisplayedTab}");
                var closing = controller.SelectAsync("inventory", forceReload: true).AsTask();
                lifetime.Cancel();
                Debug.Log($"MUI Tabs parent close: {(await closing).Status}; phase={controller.ViewModel.Snapshot.Phase}");
                await lifetime.DisposeAsync();
                Debug.Log("MUI Tabs cleanup complete");
            }
            catch (OperationCanceledException)
            {
                // 父界面关闭时停止示例流程。
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
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

        private static void AddButton(string key, string title, Transform parent, Vector2 position)
        {
            var node = Node(key, parent, new Vector2(180, 48), position);
            var image = node.gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.6f);
            node.gameObject.AddComponent<Button>().targetGraphic = image;
            AddText("Label", title, node, new Vector2(170, 45), Vector2.zero);
        }

        private static void AddTab(string key, string title, Transform parent, float x)
        {
            AddButton(key, title, parent, new Vector2(x, 0));
            var button = parent.Find(key);
            Node("Selected", button, new Vector2(170, 4), new Vector2(0, -22)).gameObject.AddComponent<Image>().color = Color.cyan;
        }

        [ContextMenu("Log Cache And Preparation State")]
        private void LogCacheAndPreparationState()
        {
            if (controller != null)
            {
                Debug.Log($"MUI Tabs cached={controller.CachedContentCount}; quarantined={controller.QuarantinedPreparationCount}; created={versionedProvider.CreatedCount}");
            }
        }

        [ContextMenu("Clear Cached Tabs")]
        private void ClearCachedTabs()
        {
            if (controller != null)
            {
                _ = ClearCachedTabsAsync();
            }
        }

        [ContextMenu("Clear Inactive Tab Content")]
        private void TrimRegisteredInactiveMemory()
        {
            if (controller != null)
            {
                _ = TrimRegisteredInactiveMemoryAsync();
            }
        }

        private async Task TrimRegisteredInactiveMemoryAsync()
        {
            try
            {
                var displayed = controller.ViewModel.Snapshot.DisplayedTab;
                await controller.ClearCacheAsync();
                Debug.Log($"MUI 页签回收：显示项={displayed} -> {controller.ViewModel.Snapshot.DisplayedTab}，" +
                    $"缓存={controller.CachedContentCount}");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        [ContextMenu("Invalidate Tab Resource Version")]
        private void InvalidateTabResourceVersion()
        {
            if (versionedProvider == null || controller == null)
            {
                return;
            }

            versionedProvider.Invalidate();
            controller.RefreshCache();
            Debug.Log("MUI 页签资源代际已变更；当前已提交页面保留，缓存和准备中的旧内容失效。");
            LogCacheAndPreparationState();
        }

        private async Task ClearCachedTabsAsync()
        {
            try
            {
                await controller.ClearCacheAsync(lifetime.Token);
                LogCacheAndPreparationState();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            _ = CleanupAsync();
        }

        private async Task CleanupAsync()
        {
            try
            {
                await lifetime.DisposeAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                if (provider != null)
                {
                    provider.Dispose();
                }

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
