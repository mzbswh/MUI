using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Navigation
{
    /// <summary>在单一 UI 线程上运行的导航器；内部提交后才执行渲染与外部回调。</summary>
    public sealed partial class Navigator : INavigator, IDisposable, IAsyncDisposable
    {
        private readonly IViewProvider provider;
        private readonly ISynchronousViewProvider synchronousProvider;
        private readonly UIUserPreferences userPreferences;
        private readonly Guid host = Guid.NewGuid();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly SemaphoreSlim requests = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly NavigationOwnership ownership = new NavigationOwnership();
        private readonly Dictionary<ViewHandle, ViewInstance> entries = new Dictionary<ViewHandle, ViewInstance>();
        private readonly Dictionary<string, Route> routes = new Dictionary<string, Route>(StringComparer.Ordinal);
        private readonly List<ViewInstance> activeOrder = new List<ViewInstance>();
        private readonly List<ViewHandle> history = new List<ViewHandle>();
        private readonly Dictionary<ViewHandle, CloseOutcome> terminal = new Dictionary<ViewHandle, CloseOutcome>();
        private readonly Queue<TerminalRecord> terminalOrder = new Queue<TerminalRecord>();
        private readonly Queue<Action> posted = new Queue<Action>();
        private readonly AsyncLocal<CallbackContext> callback = new AsyncLocal<CallbackContext>();
        private readonly int queueCapacity;
        private readonly int terminalCapacity;
        private readonly TimeSpan terminalDuration;
        private long nextHandle;
        private long order;
        private int pending;
        private Task shutdownTask;
        private ViewHandle focused;

        public Navigator(IViewProvider provider,
                    int queueCapacity = 64,
                    int terminalCapacity = 256,
                    int preloadCapacity = 32,
                    ICloseConfirmationService closeConfirmationService = null,
                    UIUserPreferences userPreferences = null,
                    int cacheCapacity = 16,
                    long? maxCachedEstimatedBytes = null,
                    int cleanupCapacity = 16,
                    TimeSpan? terminalDuration = null)
                    : this(provider, null, queueCapacity, terminalCapacity, preloadCapacity,
                        closeConfirmationService, userPreferences, cacheCapacity, maxCachedEstimatedBytes,
                        cleanupCapacity, terminalDuration)
        {
        }

        private Navigator(IViewProvider provider, ISynchronousViewProvider synchronousProvider,
                    int queueCapacity, int terminalCapacity, int preloadCapacity,
                    ICloseConfirmationService closeConfirmationService, UIUserPreferences userPreferences,
                    int cacheCapacity, long? maxCachedEstimatedBytes,
                    int cleanupCapacity, TimeSpan? terminalDuration)
        {
            if (cleanupCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cleanupCapacity));
            }

            this.cleanupCapacity = cleanupCapacity;
            if (provider == null && synchronousProvider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            this.provider = provider;
            this.synchronousProvider = synchronousProvider;
            Mode = synchronousProvider == null ? LifetimeMode.AsyncAllowed : LifetimeMode.Synchronous;
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

            this.maxCachedEstimatedBytes = maxCachedEstimatedBytes;
            this.cacheCapacity = cacheCapacity;
            this.queueCapacity = queueCapacity;
            this.terminalCapacity = terminalCapacity;
            this.terminalDuration = terminalDuration ?? TimeSpan.FromMinutes(10);
            this.preloadCapacity = preloadCapacity;

            // 纯同步预加载使用独立账本，不创建异步批次及其 Lifetime 完成信号。
            if (Mode == LifetimeMode.AsyncAllowed)
            {
                preloadBatch = new PreloadBatch();
                preloadCleanup = new Lifetime();
            }
        }

        /// <summary>宿主的生命周期模式，创建后不可切换。</summary>
        public LifetimeMode Mode
        {
            get;
        }

        public ViewHandle FocusedHandle
        {
            get
            {
                AssertThread();
                return focused;
            }
        }

        public bool IsShutdown => shutdown.IsCancellationRequested;

        public IReadOnlyList<ViewHandle> History
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
                    if (command.IsRunning && command.Source is ViewInstance source && source.Handle.Host == host)
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

        private bool WouldWaitForSelf(ViewHandle handle)
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
                if (command.IsRunning && command.Source is ViewInstance instance && instance.Handle == handle)
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
                throw new InvalidOperationException("Navigator must run on its owning UI thread and SynchronizationContext.");
            }
        }

        private void Register(Route route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (routes.TryGetValue(route.Key, out var previous))
            {
                if (!ReferenceEquals(previous, route))
                {
                    throw new NavigationPreparationRejectedException(OpenRejection.ConflictingData,
                        $"路由键 {route.Key} 已登记其他定义，请复用同一不可变 Route。");
                }
                return;
            }

            var validation = RouteGraphValidator.Validate(route);
            if (!validation.IsValid)
            {
                var issue = validation.Issues[0];
                throw new NavigationPreparationRejectedException(issue.Rejection, issue.Message);
            }
            // 先检查整张图，再一次性登记，避免失败后留下半张定义表。
            foreach (var definition in validation.Routes)
            {
                if (routes.TryGetValue(definition.Key, out previous) && !ReferenceEquals(previous, definition))
                {
                    throw new NavigationPreparationRejectedException(OpenRejection.ConflictingData,
                        $"依赖路由键 {definition.Key} 与宿主已有定义冲突。");
                }
            }
            foreach (var definition in validation.Routes)
            {
                routes[definition.Key] = definition;
            }
        }

        private OpenOutcome<TResult>? Existing<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    TViewModel assigned)
                    where TViewModel : ViewModel
        {
            var count = 0;
            foreach (var entry in entries.Values)
            {
                if (!ReferenceEquals(entry.Route, route))
                {
                    continue;
                }

                count++;
                if (route.Policy.AllowMultiple || entry.State != ViewState.Open)
                {
                    continue;
                }

                var instance = (ViewInstance<TViewModel, TArgs, TResult>)entry;
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
                    entry.State != ViewState.Open || entry.HasCloseStarted || entry.CloseRequest != null)
                {
                    return Reject<TResult>(OpenRejection.Busy);
                }

                if (!argsMatch || (assigned != null && !ReferenceEquals(instance.TypedModel, assigned)))
                {
                    return Reject<TResult>(OpenRejection.ConflictingData);
                }

                if (retiringDependencies.Contains(entry.Handle))
                {
                    return Reject<TResult>(OpenRejection.Busy);
                }

                if (!CanBringToFront(entry))
                {
                    return Reject<TResult>(OpenRejection.Busy);
                }

                ownership.AcquireExplicit(entry.Handle);
                entry.ExplicitFocusReleased = false;
                if (route.Policy.EnterHistory && !history.Contains(entry.Handle))
                {
                    history.Add(entry.Handle);
                }
                BringToFront(entry.Handle);
                return CompletedOpen(instance);
            }

            if (!HasCleanupCapacity)
            {
                return Reject<TResult>(OpenRejection.CleanupCapacity);
            }

            return count >= route.Policy.MaxInstances ? Reject<TResult>(OpenRejection.InstanceLimit) : (OpenOutcome<TResult>?)null;
        }

        internal ViewInstance<TViewModel, TArgs, TResult> NewInstance<TViewModel, TArgs, TResult>(Route<TViewModel, TArgs, TResult> route,
                    TArgs args,
                    TViewModel assigned, bool explicitOwner = true)
                    where TViewModel : ViewModel
        {
            var handle = new ViewHandle(host, ++nextHandle);
            var instance = new ViewInstance<TViewModel, TArgs, TResult>(this, route, handle, args, assigned, Mode);
            ownership.Register(handle, explicitOwner, route.Policy.Layer);
            entries.Add(handle, instance);
            return instance;
        }

        private static OpenOutcome<TResult> Reject<TResult>(OpenRejection reason, CleanupStatus cleanup = CleanupStatus.NotRequired) => new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: reason, cleanup: cleanup);

        internal IDisposable EnterCallback(ViewInstance source) => new CallbackScope(this, source);

        private sealed class CallbackContext
        {
            public ViewInstance Source;
            public CallbackContext Parent;
            public bool Active = true;
        }

        private sealed class CallbackScope : IDisposable
        {
            private readonly Navigator owner;
            private readonly CallbackContext previous;
            private readonly CallbackContext current;

            public CallbackScope(Navigator owner, ViewInstance source)
            {
                this.owner = owner;
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
            }
        }
    }
}
