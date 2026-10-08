using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MUI.Navigation;

namespace MUI.Dialogs
{
    /// <summary>
    /// 有界的串行确认服务，同一服务一次只展示一个确认框，前一个完成清理后才处理下一个。
    /// 在 Navigator 所属 UI 线程创建和调用，并保留该线程的 SynchronizationContext。
    /// </summary>
    public sealed class DialogService<TViewModel> : ICloseConfirmationService, IAsyncDisposable where TViewModel : ViewModel
    {
        private readonly AsyncLocal<RequestFrame> current = new AsyncLocal<RequestFrame>();
        private readonly Func<INavigator> resolveNavigator;
        private readonly Route<TViewModel, CloseConfirmation, bool> route;
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private readonly int capacity;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private ViewHandle activeDialog;

        /// <summary>创建确认服务，路由必须为独占实例的模态、获取焦点界面。</summary>
        /// <param name="resolveNavigator">展示时解析导航器，便于宿主与确认服务相互接入。</param>
        /// <param name="route">接收 CloseConfirmation 并返回 bool 的路由，不允许借用已有界面。</param>
        /// <param name="capacity">尚未结束的请求上限，包含正在展示及等待的请求。</param>
        public DialogService(Func<INavigator> resolveNavigator,
            Route<TViewModel, CloseConfirmation, bool> route, int capacity = 16)
        {
            this.resolveNavigator = resolveNavigator ?? throw new ArgumentNullException(nameof(resolveNavigator));
            this.route = route ?? throw new ArgumentNullException(nameof(route));
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (!route.Policy.Modal || !route.Policy.TakesFocus || !route.Policy.AllowMultiple || route.Policy.MaxInstances != 1)
            {
                throw new ArgumentException(
                    "Dialog route must be modal, take focus and use allowMultiple=true " +
                    "with maxInstances=1 to prevent borrowing an existing dialog.", nameof(route));
            }

            this.capacity = capacity;
        }

        /// <summary>仍在执行或排队的请求数量，不仅包含等待展示的请求。</summary>
        public int PendingRequests
        {
            get
            {
                RequireThread();
                return lifetime.PendingOperationCount;
            }
        }

        /// <summary>供关闭守卫使用；拒绝让当前确认框通过同一串行服务确认自己的关闭。</summary>
        public ValueTask<bool> ConfirmAsync(CloseContext context, CloseConfirmation confirmation, CancellationToken cancellationToken)
        {
            RequireThread();
            if (activeDialog.IsValid && context.Handle == activeDialog)
            {
                throw new InvalidOperationException("A dialog cannot use its own serial service to confirm its close.");
            }

            return ConfirmAsync(confirmation, cancellationToken);
        }

        /// <summary>
        /// 排队并等待确认结果。正常确认返回 true，取消按钮或无完成值的关闭返回 false。
        /// 令牌取消、打开失败或清理失败以异常报告；取消后会先关闭本服务打开的确认框。
        /// 关闭超时后继续等待实际清理再归还队列许可；非合作的清理会延长本次等待。
        /// 不允许在同一请求的执行链中等待另一个请求，以免串行队列自等待。
        /// </summary>
        public ValueTask<bool> ConfirmAsync(CloseConfirmation confirmation, CancellationToken cancellationToken = default)
        {
            RequireThread();
            if (confirmation == null)
            {
                throw new ArgumentNullException(nameof(confirmation));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (current.Value != null && current.Value.Active)
            {
                throw new InvalidOperationException("A dialog operation cannot wait for another request on the same serial dialog service.");
            }

            if (lifetime.PendingOperationCount >= capacity)
            {
                throw new InvalidOperationException("Dialog request capacity exhausted.");
            }

            return lifetime.RunAsync(token => RunAsync(confirmation, token, cancellationToken));
        }

        private async ValueTask<bool> RunAsync(CloseConfirmation confirmation, CancellationToken owner, CancellationToken caller)
        {
            var frame = new RequestFrame();
            current.Value = frame;
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(owner, caller))
                {
                    await serial.WaitAsync(linked.Token);
                    try
                    {
                        return await ShowAsync(confirmation, linked.Token);
                    }
                    finally
                    {
                        serial.Release();
                    }
                }
            }
            finally
            {
                frame.Active = false;
                current.Value = null;
            }
        }

        private async ValueTask<bool> ShowAsync(CloseConfirmation confirmation, CancellationToken token)
        {
            INavigator navigator = null;
            ViewHandle<bool> handle = default;
            Exception failure = null;
            var answer = false;
            var settled = false;
            var cleanupPending = false;
            try
            {
                token.ThrowIfCancellationRequested();
                navigator = resolveNavigator() ?? throw new InvalidOperationException("Dialog Navigator is unavailable.");
                var opened = await navigator.OpenAsync(route, confirmation, token);
                handle = opened.Handle;
                activeDialog = handle.Identity;
                token.ThrowIfCancellationRequested();
                if (!opened.IsSuccess)
                {
                    throw new InvalidOperationException($"Dialog open failed: {opened.Status}/{opened.Rejection}.", opened.Error);
                }

                var result = await handle.WaitForResultAsync(token);
                settled = true;
                cleanupPending = result.Cleanup == CleanupStatus.Pending;
                if (result.Error != null)
                {
                    throw result.Error;
                }

                // 业务结果先于退出和资源归还提交；Pending 在下方排空后才能归还串行许可。
                if (result.Cleanup != CleanupStatus.Complete && !cleanupPending)
                {
                    throw new InvalidOperationException("Dialog result arrived without completed cleanup.");
                }

                answer = result.IsCompleted && result.Value;
            }
            catch (Exception error)
            {
                failure = error;
            }

            // 取消等待不能遗留悬在源页面之上的确认框。
            // 清理不沿用已取消的调用者令牌，完成后才归还串行许可。
            if (handle.IsValid && !settled)
            {
                try
                {
                    var closed = await navigator.ForceCloseAsync(handle.Identity);
                    cleanupPending = closed.Cleanup == CleanupStatus.Pending;
                    if (closed.Status != CloseStatus.Closed && closed.Status != CloseStatus.AlreadyClosed)
                    {
                        throw new InvalidOperationException($"Dialog cleanup failed: {closed.Status}.", closed.Error);
                    }

                    if (closed.Error != null)
                    {
                        throw closed.Error;
                    }
                }
                catch (Exception cleanup)
                {
                    failure = failure == null ? cleanup : new AggregateException(failure, cleanup);
                }
            }

            // 结果提交或关闭超时后，实例仍可能占用路由容量。
            // 保留串行许可直到真实清理结束，不能让下一请求撞上尚未归还的旧实例。
            // 此等待不使用已取消的请求令牌，也不再次发起关闭或资源释放。
            if (cleanupPending)
            {
                try
                {
                    var cleaned = await navigator.WaitForCleanupAsync(handle.Identity);
                    if (cleaned.Error != null)
                    {
                        throw cleaned.Error;
                    }

                    if (cleaned.Cleanup != CleanupStatus.Complete)
                    {
                        throw new InvalidOperationException($"确认框实际清理未完成：{cleaned.Status}/{cleaned.Cleanup}。");
                    }
                }
                catch (Exception cleanup)
                {
                    failure = failure == null ? cleanup : new AggregateException(failure, cleanup);
                }
            }

            activeDialog = default;
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return answer;
        }

        /// <summary>
        /// 取消服务持有的请求并等待其清理；活动请求内不能等待自身服务销毁。
        /// 此服务不拥有 Navigator，也不会在释放时销毁宿主。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            RequireThread();
            if (current.Value != null && current.Value.Active)
            {
                return new ValueTask(Task.FromException(new InvalidOperationException("A dialog operation cannot await its own service disposal.")));
            }

            return lifetime.DisposeAsync();
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("Dialog service requires its owning UI thread and SynchronizationContext.");
            }
        }

        private sealed class RequestFrame
        {
            internal bool Active = true;
        }
    }
}
