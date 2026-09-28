using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MUI.ChildViews;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>
    /// 少量条目的非虚拟回收列表，按位置复用借用模型的子视图。
    /// 内容节点的 LayoutGroup 负责排布；本控件不接管滚动、尺寸或业务数据所有权。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class RecyclingListElement : Element, IElementBoundary, IChildViewElement
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private NestedViewElement itemTemplate;
        [SerializeField] private int capacity = 128;
        private readonly List<NestedViewElement> cells = new List<NestedViewElement>();
        private IReadOnlyObservableList<ViewModel> items;
        private Action<ListChangeSet<ViewModel>> sourceChanged;
        private object sourceSubscription;
        private long itemsAssignmentVersion;
        private List<ViewModel> snapshot = new List<ViewModel>();
        private ChildViewScope scope;
        private Lifetime lifetime;
        private Task pending;
        private bool initialized;
        private bool running;
        private bool resetting;
        private bool dirty;
        private int uiThread;
        private long revision;

        /// <summary>包含隐藏空闲项的已创建数量，最多为配置的容量。</summary>
        public int MaterializedCount => cells.Count;

        /// <summary>最近一次刷新失败；成功重试或新激活时清除。</summary>
        public Exception Error
        {
            get; private set;
        }

        /// <summary>异步宿主的准备边界；完全同步模式应使用直接返回的 Refresh。</summary>
        public Task PendingChange
        {
            get
            {
                foreach (var cell in cells)
                {
                    if (cell != null && cell.IsExecutingChild)
                    {
                        throw new InvalidOperationException("条目命令不能等待包含自身回收的列表刷新；修改集合后应直接返回。");
                    }
                }

                if (lifetime != null && lifetime.Mode == LifetimeMode.Synchronous)
                {
                    throw new InvalidOperationException("同步回收列表请使用直接返回的刷新入口，不查询任务。");
                }

                return pending ?? Task.CompletedTask;
            }
        }

        Task IChildViewElement.Preparation => pending ?? Task.CompletedTask;

        public IReadOnlyObservableList<ViewModel> Items
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

                DetachSource();
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                items = value;
                if (items != null)
                {
                    var subscription = new object();
                    sourceSubscription = subscription;
                    var beforeSubscriptionRevision = revision;
                    var handler = new Action<ListChangeSet<ViewModel>>(change =>
                    {
                        // 同一集合也可能被解除后再次绑定，单独比较集合引用不能隔离上一轮通知。
                        if (ReferenceEquals(sourceSubscription, subscription))
                        {
                            OnItemsChanged(change);
                        }
                    });
                    sourceChanged = handler;
                    try
                    {
                        value.Changed += handler;
                    }
                    catch (Exception subscriptionError)
                    {
                        if (IsCurrentItemsAssignment(assignment, activation) &&
                            ReferenceEquals(sourceSubscription, subscription) && ReferenceEquals(items, value))
                        {
                            items = null;
                            sourceChanged = null;
                            sourceSubscription = null;
                        }

                        try
                        {
                            value.Changed -= handler;
                        }
                        catch (Exception removalError)
                        {
                            throw new AggregateException("回收列表来源订阅与回滚均失败。", subscriptionError, removalError);
                        }

                        throw;
                    }

                    if (!IsCurrentItemsAssignment(assignment, activation) ||
                        !ReferenceEquals(sourceSubscription, subscription) ||
                        !ReferenceEquals(items, value))
                    {
                        value.Changed -= handler;
                        return;
                    }

                    if (revision != beforeSubscriptionRevision)
                    {
                        // 订阅回调已刷新当前来源，不能再提交订阅前读取的旧快照。
                        NotifyChanged();
                        return;
                    }

                    var subscribedVersion = value.Version;
                    if (!IsCurrentItemsAssignment(assignment, activation) || !ReferenceEquals(items, value))
                    {
                        return;
                    }

                    if (revision != beforeSubscriptionRevision)
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

                        if (revision != beforeSubscriptionRevision)
                        {
                            NotifyChanged();
                            return;
                        }
                    }
                }

                AcceptSnapshot(next);
                if (!IsCurrentItemsAssignment(assignment, activation))
                {
                    return;
                }

                NotifyChanged();
            }
        }

        protected override AccessibilityRole DefaultAccessibilityRole => AccessibilityRole.List;

        private bool IsCurrentItemsAssignment(long version, Lifetime activation) =>
            IsAlive && version == itemsAssignmentVersion && ReferenceEquals(activation, lifetime) &&
            (activation == null || !activation.IsEnded);

        /// <summary>初始化前配置空内容节点和独立的非激活子视图模板；容量超限直接拒绝，不截断数据。</summary>
        public void Configure(RectTransform contentRoot, NestedViewElement template, int maximumItems = 128)
        {
            RequireListAlive();
            if (initialized)
            {
                throw new InvalidOperationException("回收列表必须在初始化前配置。");
            }

            if (maximumItems < 1 || snapshot.Count > maximumItems)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumItems));
            }

            content = contentRoot;
            itemTemplate = template;
            capacity = maximumItems;
        }

        protected override void OnInitialize()
        {
            if (content == null || itemTemplate == null || capacity < 1 ||
                !(itemTemplate.transform is RectTransform) || itemTemplate.gameObject.activeSelf ||
                !IsOwnedDescendant(content, false) || content.childCount != 0 ||
                !IsOwnedDescendant(itemTemplate.transform, true) || itemTemplate.GetComponent<View>() != null ||
                itemTemplate.transform.IsChildOf(content) || content.IsChildOf(itemTemplate.transform))
            {
                throw new InvalidOperationException("回收列表需要自身边界内的空内容节点，以及内容节点之外的非激活 NestedViewElement 模板；引用不能穿过其他 View 或 Element 边界。");
            }

            uiThread = Thread.CurrentThread.ManagedThreadId;
            initialized = true;
            OnDispose(ReleaseCells);
        }

        /// <summary>层级归属不能只用 IsChildOf 判断；中途的子 View 或容器拥有独立的绑定与生命周期。</summary>
        private bool IsOwnedDescendant(Transform target, bool allowTargetBoundary)
        {
            if (target == null || target == transform)
            {
                return false;
            }

            for (var node = target; node != transform; node = node.parent)
            {
                if (node == null)
                {
                    return false;
                }

                if (node == target && allowTargetBoundary)
                {
                    continue;
                }

                if (node.GetComponent<View>() != null)
                {
                    return false;
                }

                foreach (var component in node.GetComponents<MonoBehaviour>())
                {
                    if (component != null && component is IElementBoundary)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        void IChildViewElement.BeginParentActivation(ChildViewScope parentScope, Lifetime parentLifetime)
        {
            RequireListAlive();
            // 父 View 在再次激活前已排空旧激活，池只保留原生节点，不保留旧来源订阅。
            ++itemsAssignmentVersion;
            DetachSource();
            snapshot.Clear();
            scope = parentScope;
            lifetime = parentLifetime;
            pending = null;
            Error = null;
            running = false;
            dirty = false;
            ++revision;
            parentLifetime.OnDispose(() =>
            {
                if (ReferenceEquals(lifetime, parentLifetime))
                {
                    ++itemsAssignmentVersion;
                    scope = null;
                    lifetime = null;
                    snapshot.Clear();
                    pending = null;
                    Error = null;
                    running = false;
                    dirty = false;
                    ++revision;
                    DetachSource();
                }
            });

            resetting = true;
            try
            {
                foreach (var cell in cells)
                {
                    if (cell == null || !cell.IsAlive)
                    {
                        throw new InvalidOperationException("回收列表的池节点已被外部销毁。");
                    }

                    ((IChildViewElement)cell).BeginParentActivation(scope, lifetime);
                    cell.gameObject.SetActive(false);
                }
            }
            finally
            {
                resetting = false;
            }
        }

        bool IChildViewElement.TryCompleteSynchronousPreparation()
        {
            if (Error != null)
            {
                ExceptionDispatchInfo.Capture(Error).Throw();
            }

            return !running && !dirty;
        }

        /// <summary>重新读取来源并刷新；同步宿主中绑定和回收均直接完成，异步宿主可等待 PendingChange。</summary>
        public void Refresh()
        {
            RequireListAlive();
            var source = items;
            var subscription = sourceSubscription;
            var assignment = itemsAssignmentVersion;
            var activation = lifetime;
            var currentRevision = revision;
            if (TryReadCurrentSnapshot(source, subscription, assignment, activation, currentRevision, out var next))
            {
                AcceptSnapshot(next);
            }
        }

        private void RequireListAlive()
        {
            if (initialized && Thread.CurrentThread.ManagedThreadId != uiThread)
            {
                throw new InvalidOperationException("回收列表只能在所属 UI 线程调用。");
            }

            RequireAlive();
            if (resetting)
            {
                throw new InvalidOperationException("不能在回收列表重置激活期间修改列表。");
            }
        }

        private List<ViewModel> ReadSnapshot(IReadOnlyList<ViewModel> source)
        {
            var next = new List<ViewModel>();
            if (source == null)
            {
                return next;
            }

            if (source.Count > capacity)
            {
                throw new InvalidOperationException("回收列表超过配置容量；长列表应使用 VirtualListElement。");
            }

            foreach (var model in source)
            {
                if (model == null || next.Count >= capacity)
                {
                    throw new InvalidOperationException("回收列表条目不能为空，且不能超过配置容量。");
                }

                next.Add(model);
            }

            return next;
        }

        private void DetachSource()
        {
            var previous = items;
            var previousHandler = sourceChanged;
            // 自定义事件移除器可能抛错或重入，先解除本控件对来源的引用。
            items = null;
            sourceChanged = null;
            sourceSubscription = null;
            if (previous != null)
            {
                previous.Changed -= previousHandler;
            }
        }

        private void OnItemsChanged(ListChangeSet<ViewModel> change)
        {
            // 已取消激活或正在退订的来源通知不得重新填充已清空的快照。
            if (items == null || scope == null || !scope.IsActive || lifetime == null || lifetime.IsEnded)
            {
                return;
            }

            RequireListAlive();
            var source = items;
            var subscription = sourceSubscription;
            var assignment = itemsAssignmentVersion;
            var activation = lifetime;
            var currentRevision = revision;
            var accepting = false;
            try
            {
                if (TryReadCurrentSnapshot(source, subscription, assignment, activation, currentRevision, out var next))
                {
                    accepting = true;
                    AcceptSnapshot(next);
                }
            }
            catch (Exception failure)
            {
                if (IsCurrentSource(source, subscription, assignment, activation, currentRevision) ||
                    (accepting && IsCurrentSourceIdentity(source, subscription, assignment, activation) &&
                    revision == currentRevision + 1))
                {
                    Error = failure;
                    ++revision;
                }

                throw;
            }
        }

        private bool TryReadCurrentSnapshot(IReadOnlyObservableList<ViewModel> source, object subscription,
            long assignment, Lifetime activation, long currentRevision, out List<ViewModel> next)
        {
            next = null;
            var before = source == null ? -1 : source.Version;
            if (!IsCurrentSource(source, subscription, assignment, activation, currentRevision))
            {
                return false;
            }

            next = ReadSnapshot(source);
            if (!IsCurrentSource(source, subscription, assignment, activation, currentRevision))
            {
                return false;
            }

            var after = source == null ? -1 : source.Version;
            if (!IsCurrentSource(source, subscription, assignment, activation, currentRevision))
            {
                return false;
            }

            if (before != after)
            {
                throw new InvalidOperationException("回收列表来源在读取快照期间发生静默变更，请重新发布集合通知。");
            }

            return true;
        }

        private bool IsCurrentSource(IReadOnlyObservableList<ViewModel> source, object subscription,
            long assignment, Lifetime activation, long currentRevision) =>
            IsCurrentSourceIdentity(source, subscription, assignment, activation) && revision == currentRevision;

        private bool IsCurrentSourceIdentity(IReadOnlyObservableList<ViewModel> source, object subscription,
            long assignment, Lifetime activation) =>
            IsCurrentItemsAssignment(assignment, activation) && ReferenceEquals(items, source) &&
            ReferenceEquals(sourceSubscription, subscription);

        private void AcceptSnapshot(List<ViewModel> next)
        {
            snapshot = next;
            ++revision;
            dirty = true;
            Error = null;
            if (!initialized || scope == null || !scope.IsActive || lifetime.IsEnded || running)
            {
                return;
            }

            if (lifetime.Mode == LifetimeMode.Synchronous)
            {
                foreach (var cell in cells)
                {
                    if (cell != null && cell.IsExecutingChild)
                    {
                        // 集合已提交，但当前命令尚未返回；下一帧直接同步协调，不启动任务。
                        return;
                    }
                }
            }

            running = true;
            var activation = lifetime;
            if (activation.Mode == LifetimeMode.Synchronous)
            {
                try
                {
                    activation.Run(token =>
                    {
                        foreach (var preparation in Reconcile(activation, token))
                        {
                            if (!((IChildViewElement)preparation.Element).TryCompleteSynchronousPreparation())
                            {
                                throw new InvalidOperationException("同步回收列表出现未完成的子视图准备。");
                            }
                        }
                    });
                }
                catch (Exception failure)
                {
                    if (ReferenceEquals(lifetime, activation) && !activation.IsEnded)
                    {
                        Error = failure;
                    }

                    throw;
                }
                finally
                {
                    if (ReferenceEquals(lifetime, activation))
                    {
                        running = false;
                    }
                }

                return;
            }

            // 先发布边界，初始化/状态回调查询时不会读到上一轮操作。
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshTask = completion.Task;
            pending = refreshTask;
            _ = CompleteRefreshAsync(activation, completion);
            // 刷新回调可能结束激活或启动新一轮操作，只观察本轮已发布的完成边界。
            if (refreshTask.IsCompleted)
            {
                refreshTask.GetAwaiter().GetResult();
            }
            else
            {
                _ = ObserveAsync(refreshTask);
            }
        }

        private async Task CompleteRefreshAsync(Lifetime activation, TaskCompletionSource<bool> completion)
        {
            try
            {
                await activation.RunAsync(async token =>
                {
                    foreach (var preparation in Reconcile(activation, token))
                    {
                        await preparation.Completion;
                    }

                    return true;
                });
                completion.TrySetResult(true);
            }
            catch (OperationCanceledException error) when (activation.IsEnded)
            {
                completion.TrySetCanceled(error.CancellationToken);
            }
            catch (Exception failure)
            {
                if (ReferenceEquals(lifetime, activation) && ReferenceEquals(pending, completion.Task) &&
                    !activation.IsEnded)
                {
                    Error = failure;
                }

                completion.TrySetException(failure);
            }
            finally
            {
                // 旧任务的完成可能晚于父级释放及下一次激活，不能覆盖新激活的刷新状态。
                if (ReferenceEquals(lifetime, activation) && ReferenceEquals(pending, completion.Task))
                {
                    running = false;
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsAlive || !dirty || running || Error != null || lifetime == null ||
                lifetime.Mode != LifetimeMode.Synchronous || scope == null || !scope.IsActive)
            {
                return;
            }

            try
            {
                AcceptSnapshot(snapshot);
            }
            catch (Exception failure)
            {
                UIErrors.Report(failure);
            }
        }

        private static async Task ObserveAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }
    }
}
