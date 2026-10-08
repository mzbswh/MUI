using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.ChildViews;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    /// <summary>通过借用的子视图实现横向、纵向列表与纵向网格的视口虚拟化。</summary>
    [DisallowMultipleComponent]
    public sealed partial class VirtualListElement : Element, IElementBoundary, IChildViewElement, IBindingRebindTarget
    {
        [SerializeField]
        private ScrollRect scrollRect = null;
        [SerializeField]
        private View itemTemplate = null;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("rowHeight")]
        private float estimatedItemExtent = 64;
        [SerializeField]
        private RectTransform.Axis scrollAxis = RectTransform.Axis.Vertical;
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
        private LifetimeScope lifetime;
        private bool running;
        private bool dirty;
        private bool initialized;
        private bool resettingActivation;
        private int first = -1;
        private int last = -1;
        private float viewportExtent;
        private Task pending;
        private PreparedRebind preparedRebind;

        private VirtualGridLayout Layout => CreateLayout(columns, overscan, rowIndex);

        public int MaterializedCount => cells.Count + failurePlaceholders.Count;

        public int FirstVisibleIndex
        {
            get
            {
                RequireListAlive();
                return snapshot.Count == 0 || !initialized ? -1 :
                    Layout.FirstVisible(snapshot.Count, ScrollOffset);
            }
        }

        /// <summary>当前列数；自动列数模式由可用宽度决定，不接受手动赋值。</summary>
        public int Columns
        {
            get => columns;
            set
            {
                RequireListAlive();
                if (automaticColumns)
                {
                    throw new InvalidOperationException("Automatic grid columns are determined by available width.");
                }
                SetColumnCount(value);
            }
        }

        public Task PendingChange
        {
            get
            {
                RequireExternalPreparationWait();
                return (pending ?? Task.CompletedTask);
            }
        }

        Task IChildViewElement.Preparation => (pending ?? Task.CompletedTask);

        public IReadOnlyObservableList<VirtualListItem> Items
        {
            get => items;
            set
            {
                RequireListAlive();
                if (preparedRebind != null)
                {
                    preparedRebind.WriteItems(value);
                    return;
                }

                SetItems(value);
            }
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.List;

        private void SetColumnCount(int value)
        {
            RequireListAlive();
            if (preparedRebind != null)
            {
                preparedRebind.WriteColumns(value);
                return;
            }
            if (value < 1 || value > maxCells || (IsHorizontal && value != 1))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (value == columns)
            {
                return;
            }

            var follow = ShouldFollowEnd();
            var anchorIndex = FirstVisibleIndex;
            var withinRow = anchorIndex < 0 ? 0 : ScrollOffset - Layout.OffsetForIndex(anchorIndex);
            var candidateMeasurements = new Dictionary<VirtualListItem, float>();
            var candidateOffsets = BuildRowIndex(snapshot, value, candidateMeasurements);
            var candidate = CreateLayout(value, overscan, candidateOffsets);
            // 换列会改变行的最大滚动轴尺寸，旧行内偏移不能越过新行。
            var candidateOffset = anchorIndex < 0 ? 0 : candidate.OffsetForIndex(anchorIndex) +
                Math.Min(withinRow, candidate.RowExtentForIndex(anchorIndex));
            if (initialized)
            {
                candidate.ContentExtent(snapshot.Count);
                candidate.GetRange(snapshot.Count, candidateOffset, ViewportExtent, out var candidateStart, out var candidateEnd);
                if (candidateEnd - candidateStart > maxCells)
                {
                    throw new InvalidOperationException("Grid exceeds the configured cell capacity.");
                }
            }

            var revision = ++sourceRevision;
            columns = value;
            measuredItems = candidateMeasurements;
            measurementCrossExtent = -1;
            rowIndex = candidateOffsets;
            first = last = -1;
            UpdateContentExtent();
            if (!IsAlive || revision != sourceRevision)
            {
                return;
            }

            if (anchorIndex >= 0 && activeReveal == null)
            {
                SetReadingOffset(follow ? EndOffset : candidateOffset);
            }

            // 原生尺寸和滚动回调可以提交新来源或新列数，不能继续处理旧候选。
            if (!IsAlive || revision != sourceRevision)
            {
                return;
            }

            RequestRefresh();
            NotifyChanged(nameof(Columns));
        }

        private void SetItems(IReadOnlyObservableList<VirtualListItem> value)
        {
            RequireListAlive();
            if (ReferenceEquals(items, value))
            {
                return;
            }

            var assignment = ++itemsAssignmentVersion;
            var activation = lifetime;
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
            var nextMeasurements = new Dictionary<VirtualListItem, float>();
            var nextOffsets = PrepareSnapshot(next, nextMeasurements);
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
                        NotifyChanged();
                        return;
                    }

                    nextOffsets = PrepareSnapshot(next, nextMeasurements);
                    if (!IsCurrentItemsAssignment(assignment, activation) || !ReferenceEquals(items, value))
                    {
                        return;
                    }

                    if (sourceRevision != beforeSubscriptionRevision)
                    {
                        NotifyChanged();
                        return;
                    }
                }
            }

            ApplyPreparedSnapshot(next, nextOffsets, candidateMeasurements: nextMeasurements, preservePosition: false);
            if (!IsCurrentItemsAssignment(assignment, activation))
            {
                return;
            }

            NotifyChanged();
        }

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

        private bool IsCurrentItemsAssignment(long version, LifetimeScope activation) =>
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
        public void Configure(ScrollRect scroll, View template, float itemExtent, int overscanRows = 2, int capacity = 128, int columnCount = 1, RectTransform.Axis axis = RectTransform.Axis.Vertical)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("Virtual list layout must be configured before initialization.");
            }

            ValidateScrollAxis(axis, columnCount);
            scrollAxis = axis;
            scrollRect = scroll;
            itemTemplate = template;
            estimatedItemExtent = itemExtent;
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

            if (estimatedItemExtent <= 0 || float.IsNaN(estimatedItemExtent) || float.IsInfinity(estimatedItemExtent) || overscan < 0 || maxCells < 1 || columns < 1 || columns > maxCells || measurementsPerFrame < 1 || maxRevealCorrections < 1 || endFollowTolerance < 0 || float.IsNaN(endFollowTolerance) || float.IsInfinity(endFollowTolerance))
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

            ValidateScrollAxis(scrollAxis, columns);
            ValidateSpacing(spacing, padding);
            uiThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            uiContext = System.Threading.SynchronizationContext.Current;
            InitializeTemplates();
            ValidateItemFailureTemplate();
            ValidateStateNodes();
            SetStatus(VirtualListStatus.Inactive);
            scrollRect.horizontal = IsHorizontal;
            scrollRect.vertical = !IsHorizontal;
            var content = scrollRect.content;
            ConfigureAxisAnchors(content);
            content.sizeDelta = Vector2.zero;
            ValidateAutomaticColumns();
            if (automaticColumns)
            {
                columns = CalculateColumnCount();
            }
            rowIndex = BuildRowIndex(snapshot, columns);
            initialized = true;
            scrollRect.onValueChanged.AddListener(OnScrolled);
            OnDispose(ReleaseList);
            InitializeScrollInput();
            InitializeViewportObserver();
            UpdateContentExtent();
        }

        void IChildViewElement.BeginParentActivation(ChildViewScope parentScope, LifetimeScope parentLifetime)
        {
            RequireListAlive();

            ++itemsAssignmentVersion;
            parentCancellation.Dispose();
            scope = parentScope;
            lifetime = parentLifetime;
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
                items = null;
                ClearItemFailures();
                snapshot.Clear();
                measuredItems.Clear();
                measurementCrossExtent = -1;
                rowIndex = null;
                keyIndices.Clear();
                intrinsicKeyIndex = true;
                selectedKey = null;
                selectedIndex = -1;
                ++sourceRevision;
                ++revealSourceRevision;
                StopReveal(VirtualListRevealStatus.Superseded);
                ++revealRevision;
                first = last = -1;
                foreach (var cell in cells)
                {
                    cell.Key = null;
                    ((IChildViewElement)cell.Element).BeginParentActivation(scope, lifetime);
                    cell.Root.gameObject.SetActive(false);
                    RequireCurrentParentActivation(parentLifetime);
                }

                UpdateContentExtent();
                RequireCurrentParentActivation(parentLifetime);
            }
            finally
            {
                resettingActivation = false;
            }

            SetStatus(VirtualListStatus.Empty);
            RequireCurrentParentActivation(parentLifetime);
        }

        private void RequireCurrentParentActivation(LifetimeScope activation)
        {
            if (!IsAlive || !ReferenceEquals(lifetime, activation) || activation.IsEnded || scope == null || !scope.IsActive)
            {
                throw new InvalidOperationException("虚拟列表的父激活已在回调中结束。");
            }
        }

        private void OnItemsChanged(ListChangeSet<VirtualListItem> change)
        {
            try
            {
                UnityMainThread.Require();
            }
            catch (InvalidOperationException error)
            {
                UIErrors.Report(error);
                return;
            }

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
            IReadOnlyDictionary<VirtualListItem, float> candidateMeasurements = null, int? candidateColumns = null)
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

            var count = candidateColumns ?? columns;
            var nextOffsets = BuildRowIndex(next, count, candidateMeasurements);
            CreateLayout(count, overscan, nextOffsets).ContentExtent(next.Count);
            return nextOffsets;
        }

        private void ApplyPreparedSnapshot(List<VirtualListItem> next, VirtualRowIndex nextOffsets,
            long? preparedVersion = null, Dictionary<VirtualListItem, float> candidateMeasurements = null,
            bool invalidateReveal = true, bool preservePosition = true)
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
            var follow = preservePosition && ShouldFollowEnd();
            var offset = oldIndex < 0 ? 0 : ScrollOffset - Layout.OffsetForIndex(oldIndex);
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

            var anchorIndex = -1;
            if (preservePosition && oldIndex >= 0)
            {
                var found = nextIndices.TryGetValue(snapshot[oldIndex].Key, out anchorIndex);
                if (!IsCurrent())
                {
                    return;
                }

                if (!found)
                {
                    anchorIndex = -1;
                    offset = 0;
                    // 使用旧顺序中仍存活的后继，再向前找前驱；不能把旧索引直接用于新数据。
                    for (var step = oldIndex + 1; step < snapshot.Count; ++step)
                    {
                        found = nextIndices.TryGetValue(snapshot[step].Key, out var candidate);
                        if (!IsCurrent())
                        {
                            return;
                        }

                        if (found)
                        {
                            anchorIndex = candidate;
                            break;
                        }
                    }

                    for (var step = oldIndex - 1; anchorIndex < 0 && step >= 0; --step)
                    {
                        found = nextIndices.TryGetValue(snapshot[step].Key, out var candidate);
                        if (!IsCurrent())
                        {
                            return;
                        }

                        if (found)
                        {
                            anchorIndex = candidate;
                        }
                    }
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
            if (invalidateReveal)
            {
                ++revealSourceRevision;
                StopReveal(VirtualListRevealStatus.Superseded);
            }
            PruneItemFailures();
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
                UpdateContentExtent();
                if (!IsAlive || revision != sourceRevision)
                {
                    return;
                }

                if (activeReveal == null)
                {
                    if (!preservePosition)
                    {
                        SetReadingOffset(0, preserveVelocity: false);
                    }
                    else if (follow)
                    {
                        SetReadingOffset(EndOffset);
                    }
                    else
                    {
                        SetReadingOffset(anchorIndex < 0 ? 0 : Layout.OffsetForIndex(anchorIndex) +
                            Math.Min(offset, Layout.RowExtentForIndex(anchorIndex)));
                    }

                    if (!IsAlive || revision != sourceRevision)
                    {
                        return;
                    }
                }
            }

            RequestRefresh(recover: true);
        }

        private float ItemExtent(int index) => EffectiveExtent(snapshot[index]);

        private VirtualRowIndex BuildRowIndex(IReadOnlyList<VirtualListItem> source, int columnCount,
                    IReadOnlyDictionary<VirtualListItem, float> candidateMeasurements = null)
        {
            if (columnCount < 1)
            {
                throw new InvalidOperationException("Virtual list column count must be positive.");
            }

            // Grid 的几何和物化单元共用同一个固定尺寸，不建立逐项尺寸索引。
            if (automaticColumns || columnCount > 1 || source.Count == 0)
            {
                return null;
            }

            var variable = measureItemExtents;
            if (!variable)
            {
                foreach (var item in source)
                {
                    if (item.Extent.HasValue)
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

            var extents = new float[source.Count];
            for (var index = 0; index < source.Count; ++index)
            {
                extents[index] = ListItemExtent(source[index], candidateMeasurements);
            }

            return new VirtualRowIndex(extents);
        }

        private void UpdateContentExtent()
        {
            if (!initialized)
            {
                return;
            }

            using var sample = cellLayoutMarker.Auto();
            scrollRect.content.SetSizeWithCurrentAnchors(scrollAxis, Layout.ContentExtent(snapshot.Count));
        }

        private void SetOffset(float offset)
        {
            var position = scrollRect.content.anchoredPosition;
            var bounded = Mathf.Clamp(offset, 0, Math.Max(0, Layout.ContentExtent(snapshot.Count) - ViewportExtent));
            if (IsHorizontal)
            {
                position.x = -bounded;
            }
            else
            {
                position.y = bounded;
            }
            scrollRect.content.anchoredPosition = position;
            scrollRect.StopMovement();
        }

        private void OnScrolled(Vector2 position)
        {
            ApplyViewportResize();
            RequestRefresh();
        }

        private void LateUpdate()
        {
            CompleteLayoutFrame();
            if (!initialized || !IsAlive)
            {
                return;
            }

            using var sample = updateMarker.Auto();
            BeginMeasurementFrame();
            if (scope == null || !scope.IsActive || Error != null)
            {
                PublishViewport();
                return;
            }

            try
            {
                CaptureViewportResize();
                ApplyViewportResize();
                UpdateAutomaticColumns();
                MeasureVisibleItems();
                RequestRefresh();
                if (IsAlive)
                {
                    PublishViewport();
                }
            }
            catch (Exception failure)
            {
                SetStatus(VirtualListStatus.Error, failure);
                UIErrors.Report(failure);
            }
        }
    }
}
