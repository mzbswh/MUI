using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace MUI
{
    public abstract partial class BindingContext<TViewModel>
        where TViewModel : ViewModel
    {
        public override LifetimeMode LifetimeMode => lifetimeMode;

        public override bool CanUnbindSynchronously => state != BindingState.Binding
                    && state != BindingState.Unbinding
                    && !freezing
                    && (session == null ? !cleanupPending : session.Commands.CanDisposeSynchronously);

        public override void SetLifetimeMode(LifetimeMode mode)
        {
            if (!Enum.IsDefined(typeof(LifetimeMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            RequireIdle();
            if (state != BindingState.Unbound || cleanupPending)
            {
                throw new InvalidOperationException("Lifetime mode must be set before binding.");
            }

            lifetimeMode = mode;
        }

        public override void Unbind()
        {
            if (!CanUnbindSynchronously)
            {
                throw new InvalidOperationException("Binding cannot unbind synchronously while construction, freezing or asynchronous work is in progress.");
            }

            ++unbindVersion;
            UnbindSynchronouslyCore();
        }

        private void UnbindSynchronouslyCore()
        {
            BeginUnbind(synchronous: true);
            if (cleanupFailure != null)
            {
                ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
            }
        }

        private void EndSession(BindingSession previous)
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
                    previous.Commands.Dispose();
                }
                catch (Exception failure)
                {
                    errors.Add(failure);
                }
            }

            CompleteSessionCleanup(errors);
        }

        /// <summary>
        /// 同步换绑，不调用 RebindAsync；清理旧会话后建立候选，失败时恢复旧模型与绑定。
        /// 含异步命令、在途命令和本会话命令内的换绑均在退订前拒绝。
        /// </summary>
        public void Rebind(TViewModel model)
        {
            ValidateRebind(model);
            if (ReferenceEquals(model, ViewModel))
            {
                return;
            }

            if (IsExecutingCommand || !CanUnbindSynchronously || (session != null && session.HasAsynchronousCommands))
            {
                throw new InvalidOperationException("Synchronous rebind requires idle synchronous commands; use RebindAsync for asynchronous command bindings.");
            }

            rebindInProgress = true;
            synchronousRebind = true;
            var version = unbindVersion;
            var previous = ViewModel;
            var wasBound = state == BindingState.Bound;
            var wasCommitted = wasBound && session.SourcesCommitted;
            try
            {
                UnbindSynchronouslyCore();
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
                    // 所有者关闭优先，不能在已解绑的界面上恢复旧模型。
                    if (version != unbindVersion)
                    {
                        UnbindSynchronouslyCore();
                        throw;
                    }

                    try
                    {
                        UnbindSynchronouslyCore();
                        RequireRebindCurrent(version);
                        ViewModel = previous;
                        BindRebindCandidate(version, wasCommitted);
                    }
                    catch (Exception rollbackFailure)
                    {
                        Exception cleanupError = null;
                        try
                        {
                            UnbindSynchronouslyCore();
                        }
                        catch (Exception error)
                        {
                            cleanupError = error;
                        }

                        if (version == unbindVersion)
                        {
                            state = BindingState.Faulted;
                        }

                        if (cleanupError != null)
                        {
                            throw new AggregateException("Rebind, restoration and cleanup failed.", failure, rollbackFailure, cleanupError);
                        }

                        throw new AggregateException("Rebind and restoration failed.", failure, rollbackFailure);
                    }

                    throw;
                }
            }
            finally
            {
                synchronousRebind = false;
                rebindInProgress = false;
            }
        }
    }
}
