using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>界面与表现模型之间的绑定会话入口，负责绑定、命令归属和解绑。</summary>
    public abstract class BindingContext : IAsyncDisposable
    {
        /// <summary>当前绑定阶段，不等同于界面的导航状态。</summary>
        public abstract BindingState State
        {
            get;
        }

        /// <summary>借用的表现模型；解绑不代表销毁模型。</summary>
        public abstract ViewModel Model
        {
            get;
        }

        /// <summary>本会话绑定的 View；直接继承此基类的上下文必须覆写。</summary>
        public virtual IView BoundView => null;

        /// <summary>当前清理任务，可用于等待命令和订阅收尾。</summary>
        public abstract Task CleanupCompletion
        {
            get;
        }

        /// <summary>当前异步调用链是否正在执行本会话命令，供宿主拒绝会等待自己的换绑。</summary>
        internal virtual bool IsExecutingCommand => false;

        /// <summary>所有调用链的在途绑定命令数量，只读取会话内部计数。</summary>
        internal virtual int ExecutingCommandCount => 0;

        /// <summary>只读取框架会话状态；外部上下文由驱动器按其实际解绑任务确认，不调用项目查询属性。</summary>
        internal virtual bool IsCleanupConfirmed => false;

        /// <summary>绑定前设置本次激活的命令目标，避免共享模型持有特定界面。</summary>
        public abstract void SetCommandTarget(ICommandTarget target);

        /// <summary>同步建立绑定，反向写入需等 CommitSourceWrites 后启用。</summary>
        public abstract void Bind();

        /// <summary>请求取消本会话命令，不等待完成，也不代替解绑。</summary>
        public abstract void CancelCommands();

        /// <summary>供生命周期驱动器退役已提交会话；不支持冻结的自定义上下文返回 false。</summary>
        internal virtual bool TryFreezeForClose() => false;

        /// <summary>在导航状态提交后、启用输入前调用。</summary>
        public abstract void CommitSourceWrites();

        /// <summary>解除绑定并等待命令收尾；调用方不得从本会话命令中等待自身清理。</summary>
        public abstract ValueTask UnbindAsync();

        public ValueTask DisposeAsync() => UnbindAsync();
    }

    /// <summary>
    /// 换绑提交要求调用 BeginRebindDetach 时立即停止旧订阅和旧 UI 写入；返回值仅等待命令及资源清理。
    /// 自定义 BindingContext 只有能保证这一顺序时才应实现此接口。
    /// </summary>
    public interface IImmediateRebindDetach
    {
        ValueTask BeginRebindDetach();
    }

    /// <summary>换绑候选的只读校验及异步目标准备；提交之前不得改写现有 View 或订阅。</summary>
    public interface IBindingRebindPreparation
    {
        ValueTask<IPreparedBindingRebind> PrepareRebindAsync(CancellationToken cancellationToken);
    }

    /// <summary>提交方法只允许同步工作；DisposeAsync 清理未接管候选或已退役的旧内容。</summary>
    public interface IPreparedBindingRebind : IAsyncDisposable
    {
        void Validate();

        void BeginCommit();

        void Commit();
    }

    /// <summary>需要异步准备的 Element 读取候选绑定值，不修改已显示状态。</summary>
    public interface IBindingRebindTarget
    {
        ValueTask<IPreparedBindingTarget> PrepareRebindAsync(
            IReadOnlyDictionary<string, object> values, CancellationToken cancellationToken);
    }

    public interface IPreparedBindingTarget : IAsyncDisposable
    {
        void Validate();

        void BeginCommit();

        void Commit();
    }

    /// <summary>
    /// 生命周期仅在 UI 线程执行。解绑先退订再排空命令工作；
    /// BuildBindings 内部请求解绑时，等待该同步回调完成绑定注册。
    /// </summary>
    public abstract partial class BindingContext<TViewModel> : BindingContext, IFreezableBindingContext, IImmediateRebindDetach, IBindingRebindPreparation where TViewModel : ViewModel
    {
        private BindingSession session;
        private BindingSession cleanupSession;
        private bool completedSessionCleanupConfirmed = true;
        private BindingState state;
        private bool committing;
        private bool freezing;
        private TaskCompletionSource<bool> deferredUnbind;
        private ICommandTarget commandTarget;
        private Task cleanup;
        private TaskCompletionSource<bool> cleanupCompletion;
        private bool cleanupPending;
        private Exception cleanupFailure;

        protected BindingContext(IView view, TViewModel viewModel)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        protected IView View
        {
            get;
        }

        public override IView BoundView => View;

        public TViewModel ViewModel
        {
            get;
        }

        public override ViewModel Model => ViewModel;

        public override BindingState State => state;

        internal override bool IsCleanupConfirmed => !cleanupPending && session == null &&
            (cleanupSession == null ? completedSessionCleanupConfirmed : cleanupSession.IsCleanupConfirmed);

        /// <summary>按需物化可重复等待的清理任务；清理状态由会话持有。</summary>
        public override Task CleanupCompletion
        {
            get
            {
                if (cleanup != null)
                {
                    return cleanup;
                }
                if (!cleanupPending)
                {
                    if (cleanupFailure == null)
                    {
                        return Task.CompletedTask;
                    }
                    cleanup = Task.FromException(cleanupFailure);
                    _ = cleanup.Exception;
                    return cleanup;
                }
                cleanupCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                cleanup = cleanupCompletion.Task;
                return cleanup;
            }
        }

        internal override bool IsExecutingCommand => session != null && session.IsExecutingCommand;

        internal override int ExecutingCommandCount => session == null ? 0 : session.ExecutingCommandCount;

        protected abstract void BuildBindings(BindingBuilder<TViewModel> builder);

        public override void SetCommandTarget(ICommandTarget target)
        {
            if (state != BindingState.Unbound)
            {
                throw new InvalidOperationException("Command target must be set before binding.");
            }

            commandTarget = target;
        }

        public override void CancelCommands()
        {
            if (session != null)
            {
                session.Commands.Cancel();
            }
        }

        /// <summary>已提交会话的单向退役；复用前必须完全解绑再重新绑定。</summary>
        public void Freeze()
        {
            if (state == BindingState.Frozen)
            {
                return;
            }

            if (freezing)
            {
                throw new InvalidOperationException("Binding lifecycle operation is in progress.");
            }

            if (state != BindingState.Bound || session == null || !session.SourcesCommitted)
            {
                throw new InvalidOperationException("Only a committed binding can be frozen.");
            }

            // 在取消或退订回调可能重入绑定前先设置状态。
            state = BindingState.Frozen;
            freezing = true;
            try
            {
                session.Freeze();
            }
            finally
            {
                freezing = false;
                // 自定义事件访问器或取消回调可能请求解绑。
                // 先完成会话退订，再等待命令并释放会话。
                if (deferredUnbind != null)
                {
                    BeginUnbind();
                }
            }
        }

        internal override bool TryFreezeForClose()
        {
            if (state == BindingState.Frozen)
            {
                return true;
            }

            if (state != BindingState.Bound || session == null || !session.SourcesCommitted)
            {
                return false;
            }

            // 提交源写入的设置器可以同步关闭界面。会话已建好所有订阅，
            // 冻结使 CommitSources 的后续写入和就绪回调立即停止。
            Freeze();
            return true;
        }

        public override void Bind()
        {
            RequireIdle();
            BindCore();
        }

        private void BindCore()
        {
            if (state == BindingState.Bound)
            {
                return;
            }

            if (state != BindingState.Unbound)
            {
                throw new InvalidOperationException($"Cannot bind in state {state}.");
            }

            if (!View.IsAlive)
            {
                throw new InvalidOperationException("Cannot bind a destroyed View.");
            }

            state = BindingState.Binding;
            var candidate = new BindingSession()
            {
                CommandTarget = commandTarget
            };
            try
            {
                if (View is IInputGestureView gestures)
                {
                    gestures.InvalidateInputGestures();
                    // 解绑先结束会话与订阅，再收尾原生编辑；收尾通知不能回写旧模型。
                    candidate.AddDetach(gestures.InvalidateInputGestures);
                }
                BuildBindings(new BindingBuilder<TViewModel>(View, ViewModel, candidate));
                session = candidate;
                state = BindingState.Bound;
            }
            catch (Exception failure)
            {
                session = candidate;
                state = BindingState.Faulted;
                BeginUnbind();
                if (!cleanupPending && cleanupFailure != null)
                {
                    throw new AggregateException("Binding and rollback failed.", failure, cleanupFailure);
                }

                throw;
            }

            if (deferredUnbind != null)
            {
                // BuildBindings 是同步的，但其设置器可能请求关闭。
                // 允许所有者释放 View 前，必须排空它已注册的全部内容。
                BeginUnbind();
                throw new OperationCanceledException("Binding was unbound while being built.");
            }
        }

        public override void CommitSourceWrites()
        {
            if (state != BindingState.Bound)
            {
                throw new InvalidOperationException("Binding is not ready.");
            }

            if (committing)
            {
                throw new InvalidOperationException("Reentrant binding commit.");
            }

            committing = true;
            try
            {
                session.CommitSources();
            }
            catch
            {
                // 设置器可能在抛出异常前已经解绑会话。
                if (state == BindingState.Bound)
                {
                    state = BindingState.Faulted;
                }

                throw;
            }
            finally
            {
                committing = false;
            }
        }

        public override ValueTask UnbindAsync()
        {
            if (state == BindingState.Binding || freezing)
            {
                if (deferredUnbind == null)
                {
                    deferredUnbind = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    cleanup = deferredUnbind.Task;
                    cleanupPending = true;
                }

                return new ValueTask(cleanup);
            }

            return new ValueTask(BeginUnbindAsync());
        }

        public ValueTask BeginRebindDetach()
        {
            RequireIdle();
            if (state != BindingState.Bound)
            {
                throw new InvalidOperationException("Only a bound context can begin rebind detachment.");
            }

            // 保留派生类的附加清理；实现必须在返回前同步退订。
            return UnbindAsync();
        }

        public async ValueTask<IPreparedBindingRebind> PrepareRebindAsync(CancellationToken cancellationToken)
        {
            RequireIdle();
            if (state != BindingState.Unbound || !View.IsAlive)
            {
                throw new InvalidOperationException("Only an unbound, live candidate can prepare a rebind.");
            }

            var validation = new BindingSession();
            var preview = new BindingPreview();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                BuildBindings(new BindingBuilder<TViewModel>(View, ViewModel, validation, preview));
                cancellationToken.ThrowIfCancellationRequested();
                await preview.PrepareAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return preview;
            }
            catch
            {
                await preview.DisposeAsync();
                throw;
            }
            finally
            {
                validation.Detach();
                await validation.Commands.DisposeAsync();
            }
        }

        private Task BeginUnbindAsync()
        {
            BeginUnbind();
            return CleanupCompletion;
        }

        private void BeginUnbind()
        {
            if (state == BindingState.Unbinding || state == BindingState.Unbound || (state == BindingState.Faulted && session == null))
            {
                return;
            }

            if (state == BindingState.Binding)
            {
                throw new InvalidOperationException("Reentrant binding lifecycle call.");
            }

            cleanupCompletion = deferredUnbind ??
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            deferredUnbind = null;
            var previous = session;
            cleanupSession = previous;
            session = null;
            cleanup = cleanupCompletion.Task;
            cleanupPending = true;
            state = BindingState.Unbinding;
            cleanupFailure = null;
            _ = EndSessionAsync(previous);
        }

        private async Task EndSessionAsync(BindingSession previous)
        {
            var errors = new List<Exception>();
            if (previous != null)
            {
                try
                {
                    previous.Detach();
                }
                catch (Exception failure)
                {
                    errors.Add(failure);
                }

                try
                {
                    await previous.Commands.DisposeAsync();
                }
                catch (Exception failure)
                {
                    errors.Add(failure);
                }
            }

            CompleteSessionCleanup(errors);
        }

        private void CompleteSessionCleanup(List<Exception> errors)
        {
            completedSessionCleanupConfirmed = cleanupSession == null || cleanupSession.IsCleanupConfirmed;
            if (completedSessionCleanupConfirmed)
            {
                cleanupSession = null;
            }
            state = errors.Count == 0 ? BindingState.Unbound : BindingState.Faulted;
            cleanupFailure = errors.Count == 0 ? null : new AggregateException("Binding cleanup failed.", errors);
            cleanupPending = false;
            var completion = cleanupCompletion;
            if (completion == null)
            {
                return;
            }
            if (cleanupFailure == null)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(cleanupFailure);
                // 即使没有等待者，也观察共享任务上的清理异常。
                _ = completion.Task.Exception;
            }
        }

        private void RequireIdle()
        {
            if (committing || freezing)
            {
                throw new InvalidOperationException("Binding lifecycle operation is in progress.");
            }
        }
    }
}
