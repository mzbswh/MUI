using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>界面与表现模型之间的绑定会话入口，负责绑定、命令归属和解绑。</summary>
    public abstract class BindingContext : ISynchronousDisposable, IAsyncDisposable
    {
        /// <summary>绑定会话允许的工作类型；自定义上下文默认只声明兼容异步的旧契约。</summary>
        public virtual LifetimeMode LifetimeMode => LifetimeMode.AsyncAllowed;

        /// <summary>同步解绑能力提示；实际调用仍须重新检查，不能据此等待在途任务。</summary>
        public virtual bool CanUnbindSynchronously => false;

        public bool CanDisposeSynchronously => CanUnbindSynchronously;

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

        /// <summary>只在绑定前设置；不支持同步模式的自定义上下文必须明确拒绝。</summary>
        public virtual void SetLifetimeMode(LifetimeMode mode)
        {
            if (mode != LifetimeMode.AsyncAllowed)
            {
                throw new NotSupportedException("This binding context does not support synchronous lifetimes.");
            }
        }

        /// <summary>同步解除绑定；不支持的实现拒绝调用，不转发到异步入口。</summary>
        public virtual void Unbind() => throw new NotSupportedException("This binding context cannot unbind synchronously.");

        public void Dispose() => Unbind();

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
    /// 生命周期仅在 UI 线程执行。解绑先退订再排空命令工作；
    /// BuildBindings 内部请求解绑时，等待该同步回调完成绑定注册。
    /// </summary>
    public abstract partial class BindingContext<TViewModel> : BindingContext, IFreezableBindingContext where TViewModel : ViewModel
    {
        private BindingSession session;
        private BindingState state;
        private bool rebindInProgress;
        private bool synchronousRebind;
        private bool committing;
        private bool freezing;
        private long unbindVersion;
        private TaskCompletionSource<bool> deferredUnbind;
        private ICommandTarget commandTarget;
        private Task cleanup;
        private TaskCompletionSource<bool> cleanupCompletion;
        private bool cleanupPending;
        private Exception cleanupFailure;
        private LifetimeMode lifetimeMode;

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
            get; private set;
        }

        public override ViewModel Model => ViewModel;

        public override BindingState State => state;

        /// <summary>仅显式读取兼容接口时物化同步清理信号，执行清理本身不依赖任务。</summary>
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
            if (state != BindingState.Unbound || rebindInProgress)
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

            if (rebindInProgress || freezing)
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

            if (state != BindingState.Bound || session == null || !session.SourcesCommitted || rebindInProgress)
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
            var candidate = new BindingSession(synchronousRebind ? LifetimeMode.Synchronous : lifetimeMode)
            {
                CommandTarget = commandTarget
            };
            try
            {
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
            ++unbindVersion;
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

        private Task BeginUnbindAsync()
        {
            BeginUnbind();
            return CleanupCompletion;
        }

        private void BeginUnbind(bool synchronous = false)
        {
            if (state == BindingState.Unbinding || state == BindingState.Unbound || (state == BindingState.Faulted && session == null))
            {
                return;
            }

            if (state == BindingState.Binding)
            {
                throw new InvalidOperationException("Reentrant binding lifecycle call.");
            }

            synchronous = synchronous || synchronousRebind || lifetimeMode == LifetimeMode.Synchronous;
            if (synchronous && session != null && !session.Commands.CanDisposeSynchronously)
            {
                throw new InvalidOperationException("Binding commands cannot be disposed synchronously.");
            }

            cleanupCompletion = deferredUnbind;
            if (!synchronous && cleanupCompletion == null)
            {
                cleanupCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            deferredUnbind = null;
            var previous = session;
            session = null;
            cleanup = cleanupCompletion == null ? null : cleanupCompletion.Task;
            cleanupPending = true;
            state = BindingState.Unbinding;
            cleanupFailure = null;
            if (synchronous)
            {
                EndSession(previous);
            }
            else
            {
                _ = EndSessionAsync(previous);
            }
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

        public async ValueTask RebindAsync(TViewModel model)
        {
            ValidateRebind(model);
            if (ReferenceEquals(model, ViewModel))
            {
                return;
            }

            if (IsExecutingCommand)
            {
                // 必须在退订和取消前拒绝；否则旧命令等待换绑，换绑又等待同一命令结束。
                throw new InvalidOperationException("Cannot await rebinding from this binding session's command. Return from the command before requesting rebind.");
            }

            rebindInProgress = true;
            var version = unbindVersion;
            var previous = ViewModel;
            var wasBound = state == BindingState.Bound;
            var wasCommitted = wasBound && session.SourcesCommitted;
            try
            {
                await BeginUnbindAsync();
                RequireRebindCurrent(version);
                ViewModel = model;
                if (!wasBound)
                {
                    return;
                }

                try
                {
                    BindRebindCandidate(version, wasCommitted);
                }
                catch (Exception failure)
                {
                    // 外部解绑优先于替换和回滚；此处恢复旧模型
                    // 会使已经被所有者关闭的 UI 再次激活。
                    if (version != unbindVersion)
                    {
                        await BeginUnbindAsync();
                        throw;
                    }

                    try
                    {
                        await BeginUnbindAsync();
                        RequireRebindCurrent(version);
                        ViewModel = previous;
                        BindRebindCandidate(version, wasCommitted);
                    }
                    catch (Exception rollbackFailure)
                    {
                        Exception cleanupFailure = null;
                        try
                        {
                            await BeginUnbindAsync();
                        }
                        catch (Exception error)
                        {
                            cleanupFailure = error;
                        }

                        if (version == unbindVersion)
                        {
                            state = BindingState.Faulted;
                        }

                        if (cleanupFailure != null)
                        {
                            throw new AggregateException("Rebind, restoration and cleanup failed.", failure, rollbackFailure, cleanupFailure);
                        }

                        throw new AggregateException("Rebind and restoration failed.", failure, rollbackFailure);
                    }

                    throw;
                }
            }
            finally
            {
                rebindInProgress = false;
            }
        }

        private void CompleteSessionCleanup(List<Exception> errors)
        {
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
                // 同步入口直接重抛；保留任务上的同一结果供异步调用者观察。
                _ = completion.Task.Exception;
            }
        }

        /// <summary>同步与异步换绑共用的绑定、源提交及版本复核步骤。</summary>
        private void BindRebindCandidate(long version, bool committed)
        {
            BindCore();
            RequireRebindCurrent(version);
            if (committed)
            {
                CommitSourceWrites();
            }

            RequireRebindCurrent(version);
        }

        private void RequireRebindCurrent(long version)
        {
            if (version != unbindVersion)
            {
                throw new OperationCanceledException("Rebinding was interrupted by unbinding.");
            }
        }

        private void ValidateRebind(TViewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            RequireIdle();
            if (state != BindingState.Bound && state != BindingState.Unbound)
            {
                throw new InvalidOperationException($"Cannot rebind in state {state}.");
            }
        }

        private void RequireIdle()
        {
            if (rebindInProgress || committing || freezing)
            {
                throw new InvalidOperationException("Binding lifecycle operation is in progress.");
            }
        }
    }
}
