using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private static void BuildVirtualList(RectTransform parent, bool measured = false)
        {
            var boundary = Node("VirtualItems", parent, new Vector2(200, 260), new Vector2(340, 0));
            var scrollNode = Node("Scroll", boundary, new Vector2(200, 260), Vector2.zero);
            var viewport = Node("Viewport", scrollNode, new Vector2(200, 260), Vector2.zero);
            viewport.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Node("Content", viewport, Vector2.zero, Vector2.zero);
            var scroll = scrollNode.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var template = Node("ListItemTemplate", boundary, new Vector2(200, 40), Vector2.zero);
            template.gameObject.SetActive(false);
            template.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.23f, 0.3f);
            template.gameObject.AddComponent<Button>();
            var selectionVisual = Node("Selection", template, Vector2.zero, Vector2.zero);
            selectionVisual.anchorMin = Vector2.zero;
            selectionVisual.anchorMax = Vector2.one;
            selectionVisual.offsetMin = selectionVisual.offsetMax = Vector2.zero;
            var selectionImage = selectionVisual.gameObject.AddComponent<Image>();
            selectionImage.color = new Color(1, 0.8f, 0.2f, 0.35f);
            selectionImage.raycastTarget = false;
            selectionVisual.gameObject.SetActive(false);
            template.gameObject.AddComponent<VirtualListItemSelection>().Configure(selectionVisual.gameObject);
            var label = AddText("ItemLabel", "Item", template, new Vector2(180, 40), Vector2.zero);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4, 0);
            label.rectTransform.offsetMax = new Vector2(-4, 0);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 24;
            if (measured)
            {
                label.resizeTextForBestFit = false;
                label.fontSize = 20;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.alignment = TextAnchor.UpperLeft;
                selectionVisual.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var layout = template.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(8, 8, 8, 8);
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            label.gameObject.AddComponent<TextElement>();
            var view = template.gameObject.AddComponent<View>();
            var list = boundary.gameObject.AddComponent<VirtualListElement>();
            list.Configure(scroll, view, 40, 1);
            list.ConfigureHeightMeasurement(measured, perFrame: 2);
            var featured = Instantiate(view, boundary, false);
            featured.name = "FeaturedItemTemplate";
            featured.GetComponent<Image>().color = new Color(0.35f, 0.2f, 0.1f);
            list.ConfigureTemplates(new[] { new VirtualListTemplate("featured", featured) });
            var loading = AddText("ListLoading", "Loading...", boundary, new Vector2(200, 30), new Vector2(0, -150)).gameObject;
            var empty = AddText("ListEmpty", "No items", boundary, new Vector2(200, 30), new Vector2(0, -150)).gameObject;
            var error = AddText("ListError", "Unable to show items", boundary, new Vector2(200, 30), new Vector2(0, -150)).gameObject;
            list.ConfigureStates(loading, empty, error);
        }

        private async Task DemonstratePagingAsync(PageViewModel model, VirtualListElement list)
        {
            var owner = new Lifetime();
            using var demoClose = cancellation.Token.Register(owner.Cancel);
            var secondPageFailed = false;
            var paged = new PagedList<VirtualListItem, int>(owner, async (cursor, size, token) =>
            {
                await Task.Delay(30, token);
                if (cursor == 20 && !secondPageFailed)
                {
                    secondPageFailed = true;
                    throw new System.InvalidOperationException("Sample page temporarily unavailable.");
                }

                var page = new List<VirtualListItem>();
                var end = System.Math.Min(45, cursor + size);
                for (var index = cursor; index < end; ++index)
                {
                    page.Add(new VirtualListItem(index, new ThingItemViewModel { Label = "Paged " + index }));
                }

                return new ListPage<VirtualListItem, int>(page, end, end < 45);
            }, item => item.Key, pageSize: 20, maxItems: 60);
            try
            {
                model.VirtualItems = paged;
                var firstPage = paged.LoadNextAsync().AsTask();
                var cancelledWait = await paged.LoadNextAsync(new System.Threading.CancellationToken(true));
                Debug.Log($"MUI Paging: first={(await firstPage).Status}; cancelledWait={cancelledWait.Status}; count={paged.Count}");
                var failed = await paged.LoadNextAsync();
                Debug.Log($"MUI Paging failed: status={failed.Status}; keptCount={paged.Count}");
                var retried = await list.LoadNextPageAsync();
                list.AutoLoadPages = true;
                await list.ScrollToKeyAsync(39);
                // 给帧驱动页尾预取一次触发机会；日志用于手动观察，不充当运行验收断言。
                await Task.Delay(150, cancellation.Token);
                await list.PendingChange;
                Debug.Log($"MUI Paging retry: status={retried.Status}; count={paged.Count}; hasMore={paged.HasMore}; cells={list.MaterializedCount}");
                list.AutoLoadPages = false;
                var history = new PagedList<VirtualListItem, int>(owner, async (cursor, size, token) =>
                {
                    await Task.Delay(30, token);
                    var start = System.Math.Max(0, cursor - size);
                    var page = new List<VirtualListItem>();
                    for (var index = start; index < cursor; ++index)
                    {
                        page.Add(new VirtualListItem(index, new ThingItemViewModel { Label = "History " + index }));
                    }

                    return new ListPage<VirtualListItem, int>(page, start, start > 0);
                }, item => item.Key, initialCursor: 100, pageSize: 20, maxItems: 100,
                    insertion: PageInsertion.Prepend);
                model.VirtualItems = history;
                await list.LoadNextPageAsync();
                await list.PendingChange;
                await list.ScrollToKeyAsync(90);
                var anchorKey = history[list.FirstVisibleIndex].Key;
                await list.LoadNextPageAsync();
                await list.PendingChange;
                Debug.Log($"MUI Paging prepend: count={history.Count}; firstKey={history[0].Key}; anchorKept={Equals(anchorKey, history[list.FirstVisibleIndex].Key)}");
                await DemonstratePagingWindowAsync(owner, model, list, PageInsertion.Append);
                await DemonstratePagingWindowAsync(owner, model, list, PageInsertion.Prepend);
                await DemonstratePagingResetAsync(owner, model, list);
            }
            finally
            {
                if (list != null && list.IsAlive)
                {
                    list.AutoLoadPages = false;
                }

                await owner.DisposeAsync();
            }
        }

        private static async Task DemonstrateVariableHeightsAsync(VirtualListElement list, PageViewModel model, ScrollRect scroll)
        {
            var values = new List<VirtualListItem>();
            for (var index = 0; index < 1000; ++index)
            {
                values.Add(new VirtualListItem(index,
                    new ThingItemViewModel { Label = "Variable " + index }, 30 + index % 3 * 20,
                    templateKey: index % 5 == 0 ? "featured" : null));
            }

            var variable = new ObservableList<VirtualListItem>(values, itemKey: item => item.Key);
            model.VirtualItems = variable;
            await list.PendingChange;
            await list.ScrollToKeyAsync(500);
            // 同一个键和模型切换回默认模板，旧模板必须先结束绑定。
            var featuredItem = variable[500];
            variable[500] = new VirtualListItem(featuredItem.Key, featuredItem.ViewModel, featuredItem.Height);
            await list.PendingChange;
            Debug.Log($"MUI Template replacement: first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
            var anchor = list.FirstVisibleIndex;
            var oldOffset = scroll.content.anchoredPosition.y;
            // 修改视口之前的行高，首个可见键应保持，滚动偏移增加同样的高度差。
            variable[0] = new VirtualListItem(variable[0].Key, variable[0].ViewModel, 130);
            await list.PendingChange;
            Debug.Log($"MUI Variable heights: anchorKept={list.FirstVisibleIndex == anchor}; offsetDelta={scroll.content.anchoredPosition.y - oldOffset}; cells={list.MaterializedCount}");
            list.Columns = 3;
            await list.PendingChange;
            Debug.Log($"MUI Variable grid: height={scroll.content.rect.height}; cells={list.MaterializedCount}; first={list.FirstVisibleIndex}");
            list.Columns = 1;
            await list.PendingChange;
        }

        private async Task DemonstrateVirtualListAsync(View view, PageViewModel model)
        {
            var list = view.GetElement<VirtualListElement>("VirtualItems");
            await list.PendingChange;
            var small = list.MaterializedCount;
            var items = PageViewModel.CreateVirtualItems(10000);
            model.VirtualItems = items;
            await list.PendingChange;
            Debug.Log($"MUI Virtual list scale: small={small}; large={list.MaterializedCount}; items={items.Count}");
            var nativeIds = new HashSet<int>();
            foreach (var native in list.GetComponentsInChildren<View>(true))
            {
                nativeIds.Add(native.GetInstanceID());
            }

            Debug.Log("MUI Virtual list native rows: " + (nativeIds.Count - 1));
            var scroll = list.GetComponentInChildren<ScrollRect>();
            model.SelectedItemKey = 9000;
            var reveal = await list.ScrollToKeyAsync(9000, focus: true);
            var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Debug.Log($"MUI Virtual reveal: status={reveal.Status}; selected={selected.GetComponentInChildren<Text>().text}; cells={list.MaterializedCount}");
            Debug.Log($"MUI Virtual selected from VM: key={list.SelectedKey}; visual={selected.GetComponent<VirtualListItemSelection>().Selected}");
            var olderReveal = list.ScrollToKeyAsync(6000, focus: true);
            var newerReveal = list.ScrollToKeyAsync(7000, focus: true);
            Debug.Log($"MUI Virtual reveal latest: old={(await olderReveal).Status}; latest={(await newerReveal).Status}");
            var clickedItem = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            clickedItem.GetComponent<Button>().onClick.Invoke();
            Debug.Log($"MUI Virtual selected by click: vm={model.SelectedItemKey}; index={list.SelectedIndex}; visual={clickedItem.GetComponent<VirtualListItemSelection>().Selected}");
            var sourceChanged = list.ScrollToKeyAsync(8000, focus: true);
            items.NotifyUpdated(0);
            Debug.Log($"MUI Virtual reveal source changed: {(await sourceChanged).Status}");
            var beforeCancelled = scroll.content.anchoredPosition;
            var cancelledReveal = await list.ScrollToKeyAsync(100, cancellationToken: new System.Threading.CancellationToken(true));
            Debug.Log($"MUI Virtual reveal cancelled: {cancelledReveal.Status}; keptOffset={beforeCancelled == scroll.content.anchoredPosition}");
            using (view.InputGate.Block("Reveal while disabled"))
            {
                Debug.Log($"MUI Virtual reveal blocked: {(await list.ScrollToKeyAsync(8000, focus: true)).Status}");
            }

            Debug.Log($"MUI Virtual reveal missing: {(await list.ScrollToKeyAsync("missing")).Status}");
            var position = scroll.content.anchoredPosition;
            position.y = 20000;
            scroll.content.anchoredPosition = position;
            scroll.onValueChanged.Invoke(Vector2.zero);
            await list.PendingChange;
            var retained = 0;
            foreach (var native in list.GetComponentsInChildren<View>(true))
            {
                if (nativeIds.Contains(native.GetInstanceID()))
                {
                    retained++;
                }
            }

            Debug.Log("MUI Virtual list reused native rows: " + (retained - 1));
            var anchor = items[list.FirstVisibleIndex].Key;
            Debug.Log($"MUI Virtual list scroll: first={list.FirstVisibleIndex}; cells={list.MaterializedCount}");
            items.Insert(0, new VirtualListItem("new", new ThingItemViewModel { Label = "Inserted" }));
            await list.PendingChange;
            Debug.Log($"MUI Virtual list anchor: kept={Equals(anchor, items[list.FirstVisibleIndex].Key)}; first={list.FirstVisibleIndex}");
            Debug.Log($"MUI Virtual selection after insert: vm={model.SelectedItemKey}; key={list.SelectedKey}; index={list.SelectedIndex}");
            scroll.content.anchoredPosition = Vector2.zero;
            scroll.onValueChanged.Invoke(Vector2.zero);
            await list.PendingChange;
            list.Columns = 3;
            await list.PendingChange;
            model.VirtualItems = PageViewModel.CreateVirtualItems(100);
            await list.PendingChange;
            var smallGrid = list.MaterializedCount;
            Debug.Log($"MUI Virtual selection removed by reset: vmCleared={model.SelectedItemKey == null}; keyCleared={list.SelectedKey == null}; index={list.SelectedIndex}");
            model.VirtualItems = items;
            await list.PendingChange;
            Debug.Log($"MUI Grid scale: small={smallGrid}; large={list.MaterializedCount}; height={scroll.content.rect.height}");
            var rowRoots = scroll.content.GetComponentsInChildren<NestedViewElement>();
            if (rowRoots.Length > 1)
            {
                var a = (RectTransform)rowRoots[0].transform;
                var b = (RectTransform)rowRoots[1].transform;
                Debug.Log($"MUI Grid geometry: width={a.rect.width}; distinctColumn={a.anchorMin.x != b.anchorMin.x}");
            }
            list.Columns = 1;
            await list.PendingChange;
            await Task.Yield();
            Debug.Log($"MUI Grid to list shrink: cells={list.MaterializedCount}; native={list.GetComponentsInChildren<View>(true).Length - 2}");
            scroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
            scroll.onValueChanged.Invoke(Vector2.zero);
            await list.PendingChange;
            await Task.Yield();
            Debug.Log($"MUI Virtual viewport shrink: cells={list.MaterializedCount}; native={list.GetComponentsInChildren<View>(true).Length - 2}");
            scroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 10000);
            try
            {
                scroll.onValueChanged.Invoke(Vector2.zero);
                await list.PendingChange;
            }
            catch (System.InvalidOperationException)
            {
                // 超大视口用于触发容量错误，下面继续展示错误占位和恢复流程。
            }
            var failedOperation = list.PendingChange;
            Debug.Log($"MUI Virtual error: status={list.Status}; errorVisible={list.transform.Find("ListError").gameObject.activeSelf}");
            scroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
            scroll.onValueChanged.Invoke(Vector2.zero);
            await Task.Yield();
            Debug.Log($"MUI Virtual error paused: sameOperation={ReferenceEquals(failedOperation, list.PendingChange)}; status={list.Status}");
            await list.RetryAsync();
            Debug.Log($"MUI Virtual retry: status={list.Status}; errorCleared={list.Error == null}; cells={list.MaterializedCount}");
            await DemonstrateVariableHeightsAsync(list, model, scroll);
            await DemonstratePagingAsync(model, list);
            await DemonstrateGroupedListAsync(model, list);
            await DemonstrateTreeListAsync(model, list);
            model.VirtualItems = items;
            await list.PendingChange;
            items.Clear();
            await list.PendingChange;
            Debug.Log($"MUI Virtual list empty: cells={list.MaterializedCount}; first={list.FirstVisibleIndex}; status={list.Status}; placeholder={list.transform.Find("ListEmpty").gameObject.activeSelf}");
            await Task.Yield();
            Debug.Log("MUI Virtual list native rows after clear: " + (list.GetComponentsInChildren<View>(true).Length - 2));
            Task observerWait = null;
            var waitWasPending = false;
            var replacedFromReady = false;
            System.ComponentModel.PropertyChangedEventHandler observer = (sender, args) =>
            {
                if (args.PropertyName != nameof(VirtualListElement.Status))
                {
                    return;
                }

                if (list.Status == VirtualListStatus.Loading && observerWait == null)
                {
                    observerWait = list.RetryAsync();
                    waitWasPending = !observerWait.IsCompleted;
                }
                if (list.Status == VirtualListStatus.Ready && !replacedFromReady)
                {
                    replacedFromReady = true;
                    list.Items = new ObservableList<VirtualListItem>();
                }
            };
            list.PropertyChanged += observer;
            try
            {
                list.Items = PageViewModel.CreateVirtualItems(20);
                await list.PendingChange;
                await observerWait;
                Debug.Log($"MUI Virtual observer reentry: waitedCurrent={ReferenceEquals(observerWait, list.PendingChange)}; pendingAtLoading={waitWasPending}; readyReplacement={replacedFromReady}; final={list.Status}; cells={list.MaterializedCount}");
            }
            finally { list.PropertyChanged -= observer; list.Items = model.VirtualItems; }
            var changedVmInCallback = false;
            System.ComponentModel.PropertyChangedEventHandler vmObserver = (sender, args) =>
            {
                if (args.PropertyName == nameof(VirtualListElement.Status) && list.Status == VirtualListStatus.Ready && !changedVmInCallback)
                {
                    changedVmInCallback = true;
                    model.VirtualItems = new ObservableList<VirtualListItem>();
                }
            };
            list.PropertyChanged += vmObserver;
            try
            {
                model.VirtualItems = PageViewModel.CreateVirtualItems(20);
                await list.PendingChange;
                Debug.Log($"MUI Binding VM reentry: changed={changedVmInCallback}; vmCount={model.VirtualItems.Count}; sameSource={ReferenceEquals(model.VirtualItems, list.Items)}; status={list.Status}; cells={list.MaterializedCount}");
            }
            finally { list.PropertyChanged -= vmObserver; }
        }
    }
}
