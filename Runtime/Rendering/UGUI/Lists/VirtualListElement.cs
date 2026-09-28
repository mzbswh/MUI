using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.ChildViews;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>通过借用的子视图实现纵向列表与网格的视口虚拟化。</summary>
    [DisallowMultipleComponent]
    public sealed partial class VirtualListElement : Element, IElementBoundary, IChildViewElement
    {
        [SerializeField]
        private ScrollRect scrollRect = null;
        [SerializeField]
        private View itemTemplate = null;
        [SerializeField]
        private float rowHeight = 64;
        [SerializeField]
        private int overscan = 2;
        [SerializeField]
        private int maxCells = 128;
        [SerializeField]
        private int columns = 1;
        private VirtualRowIndex rowIndex;
        private readonly List<Cell> cells = new List<Cell>();
        private readonly List<VirtualListItem> snapshot = new List<VirtualListItem>();
        private IReadOnlyObservableList<VirtualListItem> items;
        private Action<ListChangeSet<VirtualListItem>> itemsChanged;
        private object itemsSubscription;
        private long itemsAssignmentVersion;
        private long snapshotVersion = -1;
        private ChildViewScope scope;
        private Lifetime lifetime;
        private bool running;
        private bool dirty;
        private bool initialized;
        private bool resettingActivation;
        private int first = -1;
        private int last = -1;
        private float viewportHeight;
        private Task pending;

        private VirtualGridLayout Layout => new VirtualGridLayout(rowHeight, columns, overscan, rowIndex);

        public int MaterializedCount => cells.Count;

        public int FirstVisibleIndex
        {
            get
            {
                RequireListAlive();
                return snapshot.Count == 0 || !initialized ? -1 :
                    Layout.FirstVisible(snapshot.Count, scrollRect.content.anchoredPosition.y);
            }
        }

        public int Columns
        {
            get => columns;
            set
            {
                RequireListAlive();
                if (value < 1 || value > maxCells)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                if (value == columns)
                {
                    return;
                }

                var anchorIndex = FirstVisibleIndex;
                var withinRow = anchorIndex < 0 ? 0 : scrollRect.content.anchoredPosition.y - Layout.OffsetForIndex(anchorIndex);
                var candidateOffsets = BuildRowIndex(snapshot, value);
                var candidate = new VirtualGridLayout(rowHeight, value, overscan, candidateOffsets);
                // 换列会改变变高行的最大高度，旧行内偏移不能越过新行。
                var candidateOffset = anchorIndex < 0 ? 0 : candidate.OffsetForIndex(anchorIndex) +
                    Math.Min(withinRow, candidate.RowHeightForIndex(anchorIndex));
                if (initialized)
                {
                    candidate.ContentHeight(snapshot.Count);
                    candidate.GetRange(snapshot.Count, candidateOffset, scrollRect.viewport.rect.height, out var candidateStart, out var candidateEnd);
                    if (candidateEnd - candidateStart > maxCells)
                    {
                        throw new InvalidOperationException("Grid exceeds the configured cell capacity.");
                    }
                }

                var revision = ++sourceRevision;
                ++revealSourceRevision;
                columns = value;
                rowIndex = candidateOffsets;
                first = last = -1;
                UpdateContentHeight();
                if (!IsAlive || revision != sourceRevision)
                {
                    return;
                }

                if (anchorIndex >= 0)
                {
                    SetOffset(candidateOffset);
                }

                // 原生尺寸和滚动回调可以提交新来源或新列数，不能继续处理旧候选。
                if (!IsAlive || revision != sourceRevision)
                {
                    return;
                }

                RequestRefresh();
                NotifyChanged();
            }
        }

        public Task PendingChange
        {
            get
            {
                RequireExternalPreparationWait();
                return GetPendingChange();
            }
        }

        Task IChildViewElement.Preparation => GetPendingChange();

        public IReadOnlyObservableList<VirtualListItem> Items
        {
            get => items;
            set
            {
                RequireListAlive();
                if (ReferenceEquals(items, value))
                {
                    return;
                }

                var assignment = ++itemsAssignmentVersion;
                var activation = lifetime;
                ValidateSynchronousSource(value);
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                var candidateVersion = value == null ? -1 : value.Version;
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                var next = ReadSnapshot(value);
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                // 在更换订阅和来源身份前完成几何准入；非法候选不能拆散旧来源与旧快照。
                var nextOffsets = PrepareSnapshot(next);
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                DetachItemsSource();
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                items = value;
                try
                {
                    BindPageSource(value as IPagedListSource);
                }
                catch
                {
                    if (IsCurrentItemsAssignment(assignment, activation) && ReferenceEquals(items, value))
                    {
                        items = null;
                    }

                    throw;
                }

                if (!IsCurrentItemsAssignment(assignment, activation) || !ReferenceEquals(items, value))
                {
                    return;
                }

                if (items != null)
                {
                    var subscription = new object();
                    itemsSubscription = subscription;
                    var beforeSubscriptionRevision = sourceRevision;
                    var handler = new Action<ListChangeSet<VirtualListItem>>(change =>
                    {
                        // 版本和数量可能与新来源巧合相同，先核对订阅身份才允许增量处理。
                        if (ReferenceEquals(itemsSubscription, subscription))
                        {
                            OnItemsChanged(change);
                        }
                    });
                    itemsChanged = handler;
                    try
                    {
                        value.Changed += handler;
                    }
                    catch (Exception subscriptionError)
                    {
                        var failures = new List<Exception> { subscriptionError };
                        if (IsCurrentItemsAssignment(assignment, activation) &&
                            ReferenceEquals(itemsSubscription, subscription) && ReferenceEquals(items, value))
                        {
                            items = null;
                            itemsChanged = null;
                            itemsSubscription = null;
                            try
                            {
                                BindPageSource(null);
                            }
                            catch (Exception removalError)
                            {
                                failures.Add(removalError);
                            }
                        }

                        try
                        {
                            value.Changed -= handler;
                        }
                        catch (Exception removalError)
                        {
                            failures.Add(removalError);
                        }

                        if (failures.Count > 1)
                        {
                            throw new AggregateException("虚拟列表来源订阅与回滚均失败。", failures);
                        }

                        throw;
                    }

                    if (!IsCurrentItemsAssignment(assignment, activation) ||
                        !ReferenceEquals(itemsSubscription, subscription) ||
                        !ReferenceEquals(items, value))
                    {
                        value.Changed -= handler;
                        return;
                    }

                    if (sourceRevision != beforeSubscriptionRevision)
                    {
                        // 来源在订阅期间已通过通知提交新快照，旧候选几何也必须一起丢弃。
                        PublishPageState();
                        NotifyChanged();
                        return;
                    }

                    var subscribedVersion = value.Version;
                    if (!IsCurrentItemsAssignment(assignment, activation) || !ReferenceEquals(items, value))
                    {
                        return;
                    }

                    if (sourceRevision != beforeSubscriptionRevision)
                    {
                        PublishPageState();
                        NotifyChanged();
                        return;
                    }

                    if (subscribedVersion != candidateVersion)
                    {
                        // 自定义事件访问器可能在登记监听前修改来源，重新读取实际已订阅的版本。
                        next = ReadSnapshot(value);
                        if (!IsCurrentItemsAssignment(assignment, activation) || !ReferenceEquals(items, value))
                        {
                            return;
                        }

                        if (sourceRevision != beforeSubscriptionRevision)
                        {
                            PublishPageState();
                            NotifyChanged();
                            return;
                        }

                        nextOffsets = PrepareSnapshot(next);
                        if (!IsCurrentItemsAssignment(assignment, activation) || !ReferenceEquals(items, value))
                        {
                            return;
                        }

                        if (sourceRevision != beforeSubscriptionRevision)
                        {
                            PublishPageState();
                            NotifyChanged();
                            return;
                        }
                    }
                }

                ApplyPreparedSnapshot(next, nextOffsets);
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                PublishPageState();
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                NotifyChanged();
            }
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.List;

        private bool IsExecutingItem()
        {
            foreach (var cell in cells)
            {
                if (cell.Element != null && cell.Element.IsExecutingChild)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCurrentItemsAssignment(long version, Lifetime activation) =>
            IsAlive && version == itemsAssignmentVersion && ReferenceEquals(activation, lifetime) &&
            (activation == null || !activation.IsEnded);

        private void RequireExternalPreparationWait()
        {
            if (IsExecutingItem())
            {
                throw new InvalidOperationException("条目命令不能等待包含自身回收的列表刷新；修改集合后应直接返回。");
            }
        }

        /// <summary>在父 View 初始化前调用，或直接配置序列化引用。</summary>
        public void Configure(ScrollRect scroll, View template, float height, int overscanRows = 2, int capacity = 128, int columnCount = 1)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Virtual list layout must be configured before initialization.");
            }

            scrollRect = scroll;
            itemTemplate = template;
            rowHeight = height;
            overscan = overscanRows;
            maxCells = capacity;
            columns = columnCount;
            rowIndex = BuildRowIndex(snapshot, columns);
        }

        protected override void OnInitialize()
        {
            if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null || itemTemplate == null)
            {
                throw new InvalidOperationException("VirtualListElement requires ScrollRect, viewport, content and an inactive View template.");
            }

            if (scrollRect.GetComponent<ScrollRectElement>() != null)
            {
                throw new InvalidOperationException("VirtualListElement owns its scroll position; remove ScrollRectElement from its ScrollRect.");
            }

            if (!(itemTemplate.transform is RectTransform))
            {
                throw new InvalidOperationException("Virtual list item template requires a RectTransform.");
            }

            if (rowHeight <= 0 || float.IsNaN(rowHeight) || float.IsInfinity(rowHeight) || overscan < 0 || maxCells < 1 || columns < 1 || columns > maxCells || measurementsPerFrame < 1 || pagePrefetchItems < 0)
            {
                throw new InvalidOperationException("Invalid virtual list geometry or capacity.");
            }

            if (itemTemplate.gameObject.activeSelf || !scrollRect.transform.IsChildOf(transform) || !itemTemplate.transform.IsChildOf(transform) || itemTemplate.transform.IsChildOf(scrollRect.content))
            {
                throw new InvalidOperationException("ScrollRect and inactive template must belong to this boundary; template must be outside content.");
            }

            if (scrollRect.content.childCount != 0 || scrollRect.content.GetComponent<LayoutGroup>() != null || scrollRect.content.GetComponent<ContentSizeFitter>() != null)
            {
                throw new InvalidOperationException("Virtual list owns an empty content root without a LayoutGroup or ContentSizeFitter.");
            }

            uiThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            uiContext = System.Threading.SynchronizationContext.Current;
            InitializeTemplates();
            ValidateStateNodes();
            SetStatus(VirtualListStatus.Inactive);
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            var content = scrollRect.content;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            rowIndex = BuildRowIndex(snapshot, columns);
            initialized = true;
            scrollRect.onValueChanged.AddListener(OnScrolled);
            OnDispose(ReleaseList);
            UpdateContentHeight();
        }

        void IChildViewElement.BeginParentActivation(ChildViewScope parentScope, Lifetime parentLifetime)
        {
            if (parentLifetime.Mode == LifetimeMode.Synchronous && autoLoadPages)
            {
                throw new InvalidOperationException("Synchronous virtual lists cannot enable asynchronous pagination.");
            }

            ++itemsAssignmentVersion;
            parentCancellation.Dispose();
            scope = parentScope;
            lifetime = parentLifetime;
            synchronousRefresh = parentLifetime.Mode == LifetimeMode.Synchronous;
            synchronousRefreshFailure = null;
            synchronousRefreshCompletion = null;
            // 新激活不继承前一次刷新任务或失败结果，兼容查询按需提供完成信号。
            pending = null;
            running = false;
            resettingActivation = true;
            try
            {
                parentLifetime.OnDispose(() => ReleaseActivationReferences(parentLifetime));
                RequireCurrentParentActivation(parentLifetime);

                parentCancellation = lifetime.Token.Register(() => ParentCancelled(parentLifetime));
                RequireCurrentParentActivation(parentLifetime);
                DetachItemsSource();
                RequireCurrentParentActivation(parentLifetime);
                BindPageSource(null);
                RequireCurrentParentActivation(parentLifetime);
                items = null;
                snapshot.Clear();
                measuredItems.Clear();
                measurementWidth = -1;
                rowIndex = null;
                keyIndices.Clear();
                intrinsicKeyIndex = true;
                selectedKey = null;
                selectedIndex = -1;
                ++sourceRevision;
                ++revealSourceRevision;
                ++revealRevision;
                first = last = -1;
                foreach (var cell in cells)
                {
                    cell.Key = null;
                    ((IChildViewElement)cell.Element).BeginParentActivation(scope, lifetime);
                    cell.Root.gameObject.SetActive(false);
                    RequireCurrentParentActivation(parentLifetime);
                }

                UpdateContentHeight();
                RequireCurrentParentActivation(parentLifetime);
            }
            finally
            {
                resettingActivation = false;
            }

            SetStatus(VirtualListStatus.Empty);
            RequireCurrentParentActivation(parentLifetime);
        }

        private void RequireCurrentParentActivation(Lifetime activation)
        {
            if (!IsAlive || !ReferenceEquals(lifetime, activation) || activation.IsEnded || scope == null || !scope.IsActive)
            {
                throw new InvalidOperationException("虚拟列表的父激活已在回调中结束。");
            }
        }

        private void OnItemsChanged(ListChangeSet<VirtualListItem> change)
        {
            // 来源取消或退订时可能仍有排队通知，不能刷新已结束激活的原生节点。
            if (scope == null || !scope.IsActive || lifetime == null || lifetime.IsEnded)
            {
                return;
            }

            // 错误线程通知不能进入失败处理后再触碰状态节点。
            RequireListAlive();
            var context = CaptureChangeContext();
            try
            {
                if (TryUpdateItem(change, ref context) || !IsCurrentChange(context))
                {
                    return;
                }

                if (TryAppend(change, ref context) || !IsCurrentChange(context))
                {
                    return;
                }

                ApplyChangedSnapshot(change, ref context);
            }
            catch (Exception failure)
            {
                if (IsCurrentChangeFailure(context))
                {
                    SetStatus(VirtualListStatus.Error, failure);
                }

                throw;
            }
        }

        private static List<VirtualListItem> ReadSnapshot(IReadOnlyList<VirtualListItem> source)
        {
            var next = new List<VirtualListItem>();
            var keys = new HashSet<object>();
            if (source != null)
            {
                foreach (var item in source)
                {
                    if (item == null || !keys.Add(item.Key))
                    {
                        throw new InvalidOperationException("Virtual list requires non-null items with unique stable keys.");
                    }

                    next.Add(item);
                }
            }

            return next;
        }

        /// <summary>只校验候选并构建几何索引，不改变来源、订阅、选择和滚动位置。</summary>
        private VirtualRowIndex PrepareSnapshot(List<VirtualListItem> next,
            IReadOnlyDictionary<VirtualListItem, float> candidateMeasurements = null)
        {
            if (initialized)
            {
                ValidateItemTemplates(next);
            }

            // 错误期间来源或模型可能已变化；恢复时重新测量，不能沿用旧快照的缓存。
            if (candidateMeasurements == null && Error != null)
            {
                candidateMeasurements = new Dictionary<VirtualListItem, float>();
            }

            var nextOffsets = BuildRowIndex(next, columns, candidateMeasurements);
            new VirtualGridLayout(rowHeight, columns, overscan, nextOffsets).ContentHeight(next.Count);
            return nextOffsets;
        }

        private void ApplyPreparedSnapshot(List<VirtualListItem> next, VirtualRowIndex nextOffsets,
            long? preparedVersion = null, Dictionary<VirtualListItem, float> candidateMeasurements = null)
        {
            var assignment = itemsAssignmentVersion;
            var activation = lifetime;
            var source = items;
            var previousRevision = sourceRevision;
            var previousError = Error;
            var previousMeasurements = measuredItems;
            bool IsCurrent() => IsCurrentItemsAssignment(assignment, activation) &&
                ReferenceEquals(items, source) && sourceRevision == previousRevision &&
                ReferenceEquals(Error, previousError) && ReferenceEquals(measuredItems, previousMeasurements);

            var nextVersion = preparedVersion ?? (items == null ? -1 : items.Version);
            if (!IsCurrent())
            {
                return;
            }

            var oldIndex = initialized ? FirstVisibleIndex : -1;
            var anchor = oldIndex < 0 ? null : snapshot[oldIndex].Key;
            var offset = oldIndex < 0 ? 0 : scrollRect.content.anchoredPosition.y - Layout.OffsetForIndex(oldIndex);
            var nextIndices = new Dictionary<object, int>();
            var intrinsicKeys = true;
            for (var i = 0; i < next.Count; i++)
            {
                var key = next[i].Key;
                intrinsicKeys &= IsIntrinsicKey(key);
                nextIndices.Add(key, i);
                if (!IsCurrent())
                {
                    return;
                }
            }

            var nextMeasurements = PrepareMeasurements(next, candidateMeasurements ?? previousMeasurements,
                previousError != null);
            if (!IsCurrent())
            {
                return;
            }

            snapshot.Clear();
            snapshot.AddRange(next);
            rowIndex = nextOffsets;
            snapshotVersion = nextVersion;
            keyIndices = nextIndices;
            intrinsicKeyIndex = intrinsicKeys;
            measuredItems = nextMeasurements;
            var revision = ++sourceRevision;
            ++revealSourceRevision;
            // 调用外部选择或布局回调前先使状态失效；嵌套的增量
            // 追加不能将旧实例化范围视为与当前快照匹配。
            first = last = -1;
            ReconcileSelection();
            if (!IsAlive || revision != sourceRevision)
            {
                return;
            }

            if (initialized)
            {
                UpdateContentHeight();
                if (!IsAlive || revision != sourceRevision)
                {
                    return;
                }

                if (anchor != null)
                {
                    var index = -1;
                    for (var i = 0; i < snapshot.Count; i++)
                    {
                        var matches = Equals(snapshot[i].Key, anchor);
                        if (!IsAlive || revision != sourceRevision)
                        {
                            return;
                        }

                        if (matches)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index < 0)
                    {
                        index = Mathf.Clamp(oldIndex, 0, Math.Max(0, snapshot.Count - 1));
                    }

                    SetOffset(Layout.OffsetForIndex(index) + Math.Min(offset, snapshot.Count == 0 ? 0 : Layout.RowHeightForIndex(index)));
                    if (!IsAlive || revision != sourceRevision)
                    {
                        return;
                    }
                }
            }

            RequestRefresh(recover: true);
        }

        private float ItemHeight(int index) => EffectiveHeight(snapshot[index]);

        private VirtualRowIndex BuildRowIndex(IReadOnlyList<VirtualListItem> source, int columnCount,
                    IReadOnlyDictionary<VirtualListItem, float> candidateMeasurements = null)
        {
            var variable = measureItemHeights && source.Count != 0;
            if (!variable)
            {
                foreach (var item in source)
                {
                    if (item.Height.HasValue)
                    {
                        variable = true;
                        break;
                    }
                }
            }

            if (!variable)
            {
                return null;
            }

            if (columnCount < 1)
            {
                throw new InvalidOperationException("Virtual list column count must be positive.");
            }

            var rows = (source.Count - 1) / columnCount + 1;
            var heights = new float[rows];
            for (var row = 0; row < rows; ++row)
            {
                var height = 0f;
                var start = row * columnCount;
                var end = (int)Math.Min(source.Count, (long)start + columnCount);
                for (var index = start; index < end; ++index)
                {
                    height = Math.Max(height, EffectiveHeight(source[index], candidateMeasurements));
                }

                // 网格同一行按最高项排布，每个单元仍保留自己的显式高度。
                heights[row] = height;
            }

            return new VirtualRowIndex(heights);
        }

        private void UpdateContentHeight()
        {
            if (!initialized)
            {
                return;
            }

            scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Layout.ContentHeight(snapshot.Count));
        }

        private void SetOffset(float offset)
        {
            var position = scrollRect.content.anchoredPosition;
            position.y = Mathf.Clamp(offset, 0, Math.Max(0, Layout.ContentHeight(snapshot.Count) - scrollRect.viewport.rect.height));
            scrollRect.content.anchoredPosition = position;
            scrollRect.StopMovement();
        }

        private void OnScrolled(Vector2 position) => RequestRefresh();

        private void LateUpdate()
        {
            CompleteLayoutFrame();
            if (!initialized || !IsAlive || scope == null || !scope.IsActive || Error != null)
            {
                return;
            }

            if (viewportHeight != scrollRect.viewport.rect.height)
            {
                viewportHeight = scrollRect.viewport.rect.height;
                first = last = -1;
            }

            try
            {
                MeasureVisibleItems();
                RequestRefresh();
                TryLoadMorePages();
            }
            catch (Exception failure)
            {
                SetStatus(VirtualListStatus.Error, failure);
                UIErrors.Report(failure);
            }
        }
    }
}
