using System;
using System.Collections.Generic;

namespace MUI
{
    internal sealed partial class ViewPresenterLifecycle<TViewModel, TArgs, TResult>
        where TViewModel : ViewModel
    {
        private bool synchronousRebinding;
        private bool synchronousRebindCancelled;

        /// <summary>直接换绑并同步释放原自有模型；新模型始终借用，不转移所有权。</summary>
        internal RebindOutcome Rebind(TViewModel next, IView view, ICommandTarget target,
            Func<IView, TViewModel, BindingContext> bindingFactory, Lifetime activation,
            Action requireActive, Action committed, Action<Exception> recoveryFailed)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (activation.Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("Synchronous rebinding requires a synchronous activation.");
            }

            if (IsRebinding)
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.Busy);
            }

            if (destroyed || !opened || Binding == null)
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.SourceUnavailable);
            }

            if (Binding.IsExecutingCommand)
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.Reentrant);
            }

            if (!(view is IInputView input))
            {
                return new RebindOutcome(RebindStatus.Rejected, RebindRejection.InputControlUnsupported);
            }

            if (ReferenceEquals(next, model))
            {
                return new RebindOutcome(RebindStatus.Applied);
            }

            synchronousRebinding = true;
            synchronousRebindCancelled = false;
            var previous = model;
            var previousBinding = Binding;
            var detachStarted = false;
            var detached = false;
            var changed = false;
            var unsafeRecovery = false;
            var status = RebindStatus.Failed;
            var cleanup = RebindCleanup.NotRequired;
            var errors = new List<Exception>();
            IDisposable blocker = null;

            void RequireCurrent(bool restoring = false)
            {
                activation.Token.ThrowIfCancellationRequested();
                requireActive();
                invoke(() =>
                {
                    if (!view.IsAlive || (!restoring && synchronousRebindCancelled))
                    {
                        throw new OperationCanceledException("View became unavailable during synchronous rebinding.");
                    }
                });
                activation.Token.ThrowIfCancellationRequested();
                requireActive();
            }

            void CompleteBinding(BindingContext binding, bool restoring)
            {
                RequireCurrent(restoring);
                invoke(binding.Bind);
                RequireCurrent(restoring);
                if (view is IChildViewHost children)
                {
                    invoke(() =>
                    {
                        if (!children.TryCompleteChildPreparation())
                        {
                            throw new InvalidOperationException("Synchronous rebind cannot wait for child preparation.");
                        }
                    });
                }

                RequireCurrent(restoring);
                invoke(binding.CommitSourceWrites);
                RequireCurrent(restoring);
                CommitRebindChildren(view);
                RequireCurrent(restoring);
            }

            try
            {
                RequireCurrent();
                invoke(() =>
                {
                    if (previousBinding.State != BindingState.Bound || !previousBinding.CanUnbindSynchronously ||
                        !modelOwnership.CanDisposeSynchronously)
                    {
                        throw new InvalidOperationException("Current binding or owned model cannot be released synchronously.");
                    }
                });
                RequireCurrent();
                invoke(() => blocker = input.InputGate.Block("Rebinding view model"));
                RequireCurrent();
                detachStarted = true;
                cleanup = RebindCleanup.Complete;
                invoke(previousBinding.Unbind);
                detached = true;
                RequireCurrent();
                modelOwnership.SetModel(next);
                changed = true;
                invoke(() => presenter.ChangeViewModel(next));
                RequireCurrent();
                Binding = null;
                invoke(() => Binding = bindingFactory(view, next));
                RequireCurrent();
                invoke(() => ViewPreparation.ValidateBinding(Binding, next));
                RequireCurrent();
                invoke(() => Binding.SetLifetimeMode(LifetimeMode.Synchronous));
                RequireCurrent();
                invoke(() => Binding.SetCommandTarget(target));
                CompleteBinding(Binding, false);
                committed();
                status = RebindStatus.Applied;
                invoke(modelOwnership.ReleaseReplacedModel);
            }
            catch (Exception error)
            {
                errors.Add(error);
                if ((detachStarted && !detached) || status == RebindStatus.Applied)
                {
                    cleanup = RebindCleanup.Failed;
                }

                if (status != RebindStatus.Applied)
                {
                    status = error is OperationCanceledException ? RebindStatus.Cancelled : RebindStatus.Failed;
                    unsafeRecovery = detachStarted && !detached;
                    if (detached)
                    {
                        if (Binding != null && !ReferenceEquals(Binding, previousBinding))
                        {
                            try
                            {
                                invoke(Binding.Unbind);
                            }
                            catch (Exception failure)
                            {
                                errors.Add(failure);
                                cleanup = RebindCleanup.Failed;
                                unsafeRecovery = true;
                            }
                        }

                        Binding = previousBinding;
                        if (changed)
                        {
                            modelOwnership.SetModel(previous);
                            try
                            {
                                invoke(() => presenter.ChangeViewModel(previous));
                            }
                            catch (Exception failure)
                            {
                                errors.Add(failure);
                                unsafeRecovery = true;
                            }
                        }

                        if (!unsafeRecovery)
                        {
                            try
                            {
                                RequireCurrent(true);
                                invoke(() => ViewPreparation.ValidateBinding(previousBinding, previous));
                                CompleteBinding(previousBinding, true);
                            }
                            catch (Exception failure)
                            {
                                errors.Add(failure);
                                unsafeRecovery = true;
                            }
                        }
                    }
                }
            }
            finally
            {
                // 换绑已停止触碰模型和绑定后，宿主才可以同步执行故障关闭。
                synchronousRebinding = false;
                synchronousRebindCancelled = false;
            }

            var recoveryNotified = false;
            var recoveryAttempted = false;
            void CloseUnsafeView()
            {
                recoveryAttempted = true;
                var failure = CombineRebindErrors(errors);
                completedRebind = new RebindOutcome(status, error: failure, recoveryFailed: true, cleanup: cleanup);
                try
                {
                    recoveryFailed(failure);
                    recoveryNotified = true;
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (unsafeRecovery)
            {
                CloseUnsafeView();
            }

            // 故障关闭失败时保留输入屏障，不能重新开放已损坏的视图。
            if (blocker != null && (!unsafeRecovery || recoveryNotified))
            {
                try
                {
                    invoke(blocker.Dispose);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                    unsafeRecovery = true;
                    cleanup = RebindCleanup.Failed;
                }
            }

            if (unsafeRecovery && !recoveryAttempted)
            {
                CloseUnsafeView();
            }

            var combined = CombineRebindErrors(errors);
            if (cleanup == RebindCleanup.Failed && rebindCleanupFailure == null)
            {
                rebindCleanupFailure = combined;
            }

            var outcome = new RebindOutcome(status, error: combined, recoveryFailed: unsafeRecovery, cleanup: cleanup);
            completedRebind = outcome;
            return outcome;
        }
    }
}
