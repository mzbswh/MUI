using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Pages
{
    /// <summary>在单一 UI 线程上运行的页面管理器；内部提交后才执行渲染与外部回调。</summary>
    public sealed partial class PageManager : IPageManager, IAsyncDisposable
    {
        private readonly IViewProvider provider;
        private readonly UIUserPreferences userPreferences;
        private readonly Guid host = Guid.NewGuid();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly SemaphoreSlim requests = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly PageOwnership ownership = new PageOwnership();
        private readonly Dictionary<PageHandle, PageInstance> entries = new Dictionary<PageHandle, PageInstance>();
        private readonly Dictionary<string, PageDefinition> routes = new Dictionary<string, PageDefinition>(StringComparer.Ordinal);
        private readonly List<PageInstance> activeOrder = new List<PageInstance>();
        private readonly List<PageHandle> history = new List<PageHandle>();
        private readonly Dictionary<PageHandle, CloseOutcome> terminal = new Dictionary<PageHandle, CloseOutcome>();
        private readonly Queue<TerminalRecord> terminalOrder = new Queue<TerminalRecord>();
        private readonly Queue<Action> posted = new Queue<Action>();
        private readonly AsyncLocal<CallbackContext> callback = new AsyncLocal<CallbackContext>();
        private readonly int queueCapacity;
        private readonly int terminalCapacity;
        private readonly TimeSpan terminalDuration;
        private long nextHandle;
        // 只由导航请求和显式置前递增，不由加载、激活或显示完成分配。
        private long order;
        private int pending;
        private Task shutdownTask;
        private PageHandle focused;

        public PageManager(IViewProvider provider,
                    int queueCapacity = 64,
                    int terminalCapacity = 256,
                    int preloadCapacity = 32,
                    ICloseConfirmationService closeConfirmationService = null,
                    UIUserPreferences userPreferences = null,
                    int cacheCapacity = 16,
                    long? maxCachedEstimatedBytes = null,
                    int cleanupCapacity = 16,
                    TimeSpan? terminalDuration = null,
                    RenderOrderOptions renderOrder = null)
        {
            if (cleanupCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cleanupCapacity));
            }

            renderOrdering = new PageRenderOrderAllocator(renderOrder);
            this.cleanupCapacity = cleanupCapacity;
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            this.provider = provider;
            this.closeConfirmationService = closeConfirmationService;
            this.userPreferences = userPreferences;
            if (queueCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(queueCapacity));
            }

            if (terminalCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(terminalCapacity));
            }

            if (terminalDuration.HasValue && terminalDuration.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(terminalDuration));
            }

            if (preloadCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(preloadCapacity));
            }

            if (cacheCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
            }

            if (maxCachedEstimatedBytes.HasValue && maxCachedEstimatedBytes.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCachedEstimatedBytes));
            }

            cacheBudget = new PageCacheBudget(maxCachedEstimatedBytes);
            cachedContents = new PageCacheCatalog(cacheCapacity);
            this.queueCapacity = queueCapacity;
            this.terminalCapacity = terminalCapacity;
            this.terminalDuration = terminalDuration ?? TimeSpan.FromMinutes(10);
            this.preloadCapacity = preloadCapacity;

            preloadBatch = new PreloadBatch();
            preloadCleanup = new LifetimeScope();
        }

        public PageHandle FocusedHandle
        {
            get
            {
                AssertThread();
                return focused;
            }
        }

        public bool IsShutdown => shutdown.IsCancellationRequested;

        /// <summary>当前显式打开页面的请求顺序记录；关闭后移除，不参与返回目标选择。</summary>
        public IReadOnlyList<PageHandle> History
        {
            get
            {
                AssertThread();
                return history.ToArray();
            }
        }

        private bool IsSourceCommandRunning
        {
            get
            {
                for (var command = CommandContext.Current; command != null; command = command.ExecutionParent)
                {
                    if (command.IsRunning && command.Source is PageInstance source && source.Handle.Host == host)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private CallbackContext CurrentCallback
        {
            get
            {
                for (var current = callback.Value; current != null; current = current.Parent)
                {
                    if (current.Active)
                    {
                        return current;
                    }
                }

                return null;
            }
        }

        private bool IsReentrant => CurrentCallback != null || IsRebindingSourceCommand;

        private bool WouldWaitForSelf(PageHandle handle)
        {
            for (var current = closeEvaluation.Value; current != null; current = current.Parent)
            {
                if (current.Active && current.Handle == handle)
                {
                    return true;
                }
            }

            for (var current = callback.Value; current != null; current = current.Parent)
            {
                if (current.Active && current.Source != null && current.Source.Handle == handle)
                {
                    return true;
                }
            }

            for (var command = CommandContext.Current; command != null; command = command.ExecutionParent)
            {
                if (command.IsRunning && command.Source is PageInstance instance && instance.Handle == handle)
                {
                    return true;
                }
            }

            return false;
        }

        private void AssertThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("PageManager must run on its owning UI thread and SynchronizationContext.");
            }
        }

        private void Register(PageDefinition route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (routes.TryGetValue(route.Key, out var previous))
            {
                if (!ReferenceEquals(previous, route))
                {
                    throw new PagePreparationRejectedException(OpenRejection.ConflictingData,
                        $"页面定义键 {route.Key} 已登记其他定义，请复用同一不可变 PageDefinition。");
                }
                return;
            }

            var validation = PageGraphValidator.Validate(route);
            if (!validation.IsValid)
            {
                var issue = validation.Issues[0];
                throw new PagePreparationRejectedException(issue.Rejection, issue.Message);
            }
            // 先检查整张图，再一次性登记，避免失败后留下半张定义表。
            foreach (var definition in validation.Definitions)
            {
                if (routes.TryGetValue(definition.Key, out previous) && !ReferenceEquals(previous, definition))
                {
                    throw new PagePreparationRejectedException(OpenRejection.ConflictingData,
                        $"依赖页面定义键 {definition.Key} 与宿主已有定义冲突。");
                }
            }
            foreach (var definition in validation.Definitions)
            {
                routes[definition.Key] = definition;
            }
        }

        /// <summary>返回已有实例或拒绝原因；null 表示可以登记新候选并预留额度。</summary>
        private OpenOutcome<TResult>? ResolveOpenAdmission<TViewModel, TArgs, TResult>(PageDefinition<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    TViewModel assigned, PageHandle parentPage)
                    where TViewModel : ViewModel
        {
            var count = 0;
            foreach (var entry in entries.Values)
            {
                if (!ReferenceEquals(entry.Definition, route) || !OccupiesInstanceSlot(entry))
                {
                    continue;
                }

                count++;
                if (route.Policy.AllowMultiple || route.Policy.ExistingInstance != ExistingInstancePolicy.ReturnReady ||
                    entry.State != PageState.Open)
                {
                    continue;
                }

                var instance = (PageInstance<TViewModel, TArgs, TResult>)entry;
                if (!entry.TryGetReadiness(out var readiness) || !readiness.IsReady ||
                    entry.IsRebinding || entry.IsUpdatingArgs || entry.CloseRequest != null ||
                    retiringDependencies.Contains(entry.Handle) || replacing.Contains(entry.Handle))
                {
                    return Reject<TResult>(OpenRejection.Busy);
                }

                bool argsMatch;
                using (EnterCallback(entry))
                {
                    argsMatch = route.ArgsEqual(instance.Args, args);
                }

                if (IsShutdown)
                {
                    return new OpenOutcome<TResult>(OpenStatus.HostClosed);
                }

                if (!entries.TryGetValue(entry.Handle, out var current) || !ReferenceEquals(current, entry) ||
                    entry.State != PageState.Open || entry.HasCloseStarted || entry.CloseRequest != null ||
                    entry.IsRebinding || entry.IsUpdatingArgs || retiringDependencies.Contains(entry.Handle) ||
                    replacing.Contains(entry.Handle))
                {
                    return Reject<TResult>(OpenRejection.Busy);
                }

                if (!argsMatch || (assigned != null && !ReferenceEquals(instance.TypedModel, assigned)))
                {
                    return Reject<TResult>(OpenRejection.ConflictingData);
                }

                if (entry.ParentPage != parentPage)
                {
                    return Reject<TResult>(OpenRejection.ConflictingData);
                }
                if (entry.PendingCloseIntent.HasValue || pageDepartures.Contains(entry.Handle))
                {
                    return Reject<TResult>(OpenRejection.Busy);
                }
                // 显式取得已存在实例的持有关系，不重新打开、置顶或修改历史。
                ownership.AcquireExplicit(entry.Handle);
                return CompletedOpen(instance);
            }

            if (!HasCleanupCapacity)
            {
                return Reject<TResult>(OpenRejection.CleanupCapacity);
            }

            // 满额与暂时忙碌使用不同结果；CloseOldest 仍由外层替换流程处理。
            return count >= route.Policy.MaxInstances
                ? Reject<TResult>(OpenRejection.InstanceLimit)
                : (OpenOutcome<TResult>?)null;
        }

        /// <summary>关闭提交释放逻辑额度；尚未归还的资源由独立清理账本持有。</summary>
        private bool OccupiesInstanceSlot(PageInstance instance)
        {
            if (instance.State == PageState.Open)
            {
                return true;
            }

            if (instance.State != PageState.Opening)
            {
                return false;
            }

            // 同 PageDefinition 替换候选借用仍存活源页的额度；源页先关闭时，候选自动独立占额。
            return !entries.TryGetValue(instance.ReplacementSource, out var source) ||
                source.State != PageState.Open || !ReferenceEquals(source.Definition, instance.Definition);
        }

        internal PageInstance<TViewModel, TArgs, TResult> NewInstance<TViewModel, TArgs, TResult>(PageDefinition<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    TViewModel assigned, long navigationOrder, bool explicitOwner = true)
                    where TViewModel : ViewModel
        {
            var handle = new PageHandle(host, ++nextHandle);
            var instance = new PageInstance<TViewModel, TArgs, TResult>(this, route, handle, args, assigned)
            {
                Order = navigationOrder,
                HistoryOrder = navigationOrder
            };
            ReserveRenderOrder(instance);
            ownership.Register(handle, explicitOwner, route.Policy.Layer);
            entries.Add(handle, instance);
            return instance;
        }

        private static OpenOutcome<TResult> Reject<TResult>(OpenRejection reason, CleanupStatus cleanup = CleanupStatus.NotRequired) => new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: reason, cleanup: cleanup);

        internal IDisposable EnterCallback(PageInstance source, [CallerMemberName] string phase = null) =>
            new CallbackScope(this, source, phase);

        private sealed class CallbackContext
        {
            public PageInstance Source;
            public CallbackContext Parent;
            public bool Active = true;
        }

        private sealed class CallbackScope : IDisposable
        {
            private readonly PageManager owner;
            private readonly CallbackContext previous;
            private readonly CallbackContext current;
            private readonly IDisposable diagnostic;

            public CallbackScope(PageManager owner, PageInstance source, string phase)
            {
                this.owner = owner;
                var existing = UIErrors.CurrentContext;
                diagnostic = UIErrors.BeginContext(new UIErrorContext(owner.host, source == null ? 0 : source.Handle.Id,
                    source == null ? null : source.Definition.Key, existing.Operation ?? phase, existing.Phase ?? phase));
                previous = owner.callback.Value;
                current = new CallbackContext
                {
                    Source = source,
                    Parent = previous
                };
                owner.callback.Value = current;
            }

            public void Dispose()
            {
                current.Active = false;
                owner.callback.Value = previous;
                diagnostic.Dispose();
            }
        }
    }
}
