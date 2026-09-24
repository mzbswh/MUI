using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    internal sealed partial class ViewPresenterLifecycle<TViewModel, TArgs, TResult>
        where TViewModel : ViewModel
    {
        private CancellationTokenSource rebindCancellation;
        private Exception rebindCancellationError;
        private Exception rebindCleanupFailure;
        private RebindOutcome? completedRebind;

        internal Task<RebindOutcome> Rebinding
        {
            get; private set;
        }

        internal bool IsRebinding => synchronousRebinding || (Rebinding != null && !Rebinding.IsCompleted);

        /// <summary>正常关闭守卫可取消换绑；取消不等于释放，关闭仍须等待 Rebinding。</summary>
        internal void CancelRebind()
        {
            if (synchronousRebinding)
            {
                synchronousRebindCancelled = true;
                return;
            }

            if (!IsRebinding)
            {
                return;
            }

            try
            {
                rebindCancellation.Cancel();
            }
            catch (Exception error)
            {
                if (IsRebinding)
                {
                    rebindCancellationError = error;
                }
                else
                {
                    UIErrors.Report(error);
                }
            }
        }

        /// <summary>
        /// 活动实例换绑。新模型只借用；宿主提供有效性检查、提交版本与故障关闭入口。
        /// 调用前须排除参数更新、关闭守卫与宿主回调重入；此处额外排除会话命令自等待。
        /// </summary>
        internal ValueTask<RebindOutcome> RebindAsync(TViewModel next, IView view, ICommandTarget target,
            Func<IView, TViewModel, BindingContext> bindingFactory, Lifetime activation,
            Action requireActive, Action committed, Action<Exception> recoveryFailed, CancellationToken token)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (IsRebinding)
            {
                return RejectedRebind(RebindRejection.Busy);
            }

            if (destroyed || !opened || Binding == null)
            {
                return RejectedRebind(RebindRejection.SourceUnavailable);
            }

            if (ReferenceEquals(next, model))
            {
                return new ValueTask<RebindOutcome>(new RebindOutcome(RebindStatus.Applied));
            }

            if (Binding.IsExecutingCommand)
            {
                return RejectedRebind(RebindRejection.Reentrant);
            }

            if (!(view is IInputView input))
            {
                return RejectedRebind(RebindRejection.InputControlUnsupported);
            }

            rebindCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, activation.Token);
            rebindCancellationError = null;
            var completion = new TaskCompletionSource<RebindOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            // 必须先发布信号；输入门控、解绑和业务钩子都可能同步请求关闭。
            Rebinding = completion.Task;
            _ = ExecuteRebindAsync(next, view, input, target, bindingFactory, activation,
                requireActive, committed, recoveryFailed, rebindCancellation, completion);
            return new ValueTask<RebindOutcome>(completion.Task);
        }

        private async Task ExecuteRebindAsync(TViewModel next, IView view, IInputView input,
                    ICommandTarget target, Func<IView, TViewModel, BindingContext> bindingFactory,
                    Lifetime activation, Action requireActive, Action committed, Action<Exception> recoveryFailed,
                    CancellationTokenSource cancellation, TaskCompletionSource<RebindOutcome> completion)
        {
            var previous = model;
            var previousBinding = Binding;
            var detached = false;
            var detachStarted = false;
            var changed = false;
            var unsafeRecovery = false;
            var status = RebindStatus.Failed;
            var cleanup = RebindCleanup.NotRequired;
            var errors = new List<Exception>();
            IDisposable blocker = null;

            void RequireActiveView()
            {
                activation.Token.ThrowIfCancellationRequested();
                requireActive();
                invoke(() =>
                {
                    if (!view.IsAlive)
                    {
                        throw new OperationCanceledException("View was destroyed during rebinding.");
                    }
                });
                activation.Token.ThrowIfCancellationRequested();
                requireActive();
            }

            void RequireCurrent()
            {
                cancellation.Token.ThrowIfCancellationRequested();
                RequireActiveView();
                cancellation.Token.ThrowIfCancellationRequested();
            }

            try
            {
                RequireCurrent();
                // 自定义上下文的状态读取也是外部代码，必须在排空信号发布后执行。
                invoke(() =>
                {
                    if (previousBinding.State != BindingState.Bound)
                    {
                        throw new InvalidOperationException("Only a bound active view can rebind.");
                    }
                });
                RequireCurrent();
                invoke(() => blocker = input.InputGate.Block("Rebinding view model"));
                RequireCurrent();
                detachStarted = true;
                cleanup = RebindCleanup.Complete;
                await invokeAsync(previousBinding.UnbindAsync);
                detached = true;
                RequireCurrent();

                // 先同步两处模型引用，再通知业务；失败时反向调用同一钩子。
                modelOwnership.SetModel(next);
                changed = true;
                invoke(() => presenter.ChangeViewModel(next));
                RequireCurrent();
                Binding = null;
                invoke(() => Binding = bindingFactory(view, next));
                RequireCurrent();
                invoke(() => ViewPreparation.ValidateBinding(Binding, next));
                RequireCurrent();
                invoke(() => Binding.SetCommandTarget(target));
                RequireCurrent();
                invoke(Binding.Bind);
                RequireCurrent();
                await CompleteRebindChildrenAsync(view, cancellation.Token);
                RequireCurrent();
                invoke(Binding.CommitSourceWrites);
                RequireCurrent();
                CommitRebindChildren(view);
                RequireCurrent();
                committed();
                status = RebindStatus.Applied;

                // 提交后才释放原工厂模型；失败只报告清理错误，不回滚已生效的新模型。
                await invokeAsync(modelOwnership.ReleaseReplacedModelAsync);
            }
            catch (Exception error)
            {
                if ((detachStarted && !detached) || status == RebindStatus.Applied)
                {
                    cleanup = RebindCleanup.Failed;
                }

                if (cleanup == RebindCleanup.Failed || !(error is OperationCanceledException) || !cancellation.IsCancellationRequested)
                {
                    errors.Add(error);
                }

                if (status != RebindStatus.Applied)
                {
                    status = error is OperationCanceledException ? RebindStatus.Cancelled : RebindStatus.Failed;
                    unsafeRecovery = detachStarted && !detached;
                    if (detached)
                    {
                        // 清理部分建立的新绑定，即使关闭已请求也不能遗失候选。
                        if (Binding != null && !ReferenceEquals(Binding, previousBinding))
                        {
                            try
                            {
                                await invokeAsync(Binding.UnbindAsync);
                            }
                            catch (Exception cleanupError)
                            {
                                errors.Add(cleanupError);
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
                            catch (Exception rollbackError)
                            {
                                errors.Add(rollbackError);
                                unsafeRecovery = true;
                            }
                        }

                        if (!unsafeRecovery)
                        {
                            try
                            {
                                // 调用者取消仍需恢复活动界面；已关闭/停用则只恢复引用，不再激活 UI。
                                RequireActiveView();
                                invoke(() => ViewPreparation.ValidateBinding(previousBinding, previous));
                                RequireActiveView();
                                invoke(previousBinding.Bind);
                                RequireActiveView();
                                await CompleteRebindChildrenAsync(view, activation.Token);
                                RequireActiveView();
                                invoke(previousBinding.CommitSourceWrites);
                                RequireActiveView();
                                CommitRebindChildren(view);
                                RequireActiveView();
                            }
                            catch (OperationCanceledException) when (activation.IsEnded)
                            {
                                // 已退役实例由统一激活清理排空恢复过程中创建的绑定。
                            }
                            catch (Exception rollbackError)
                            {
                                errors.Add(rollbackError);
                                unsafeRecovery = true;
                            }
                        }
                    }
                }
            }
            finally
            {
                // 无法恢复时先关闭宿主，再解除临时门控，不能短暂暴露已经损坏的绑定。
                var notifiedFailure = false;
                if (unsafeRecovery)
                {
                    try
                    {
                        recoveryFailed(CombineRebindErrors(errors));
                        notifiedFailure = true;
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }

                if (blocker != null)
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

                if (rebindCancellationError != null)
                {
                    errors.Add(rebindCancellationError);
                    cleanup = RebindCleanup.Failed;
                }

                var failure = CombineRebindErrors(errors);
                if (unsafeRecovery && !notifiedFailure)
                {
                    try
                    {
                        recoveryFailed(failure);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                        failure = CombineRebindErrors(errors);
                    }
                }

                cancellation.Dispose();
                rebindCancellation = null;
                rebindCancellationError = null;
                if (cleanup == RebindCleanup.Failed && rebindCleanupFailure == null)
                {
                    // 后续成功换绑不能抹掉已发生的资源清理失败；只保留首个失败，避免无限历史。
                    rebindCleanupFailure = failure;
                }

                var outcome = new RebindOutcome(status, error: failure, recoveryFailed: unsafeRecovery, cleanup: cleanup);
                completedRebind = outcome;
                completion.TrySetResult(outcome);
            }
        }

        private ValueTask CompleteRebindChildrenAsync(IView view, CancellationToken token) =>
                    view is IChildViewHost child ? invokeAsync(() => child.CompleteChildPreparationAsync(token)) : default;

        private void CommitRebindChildren(IView view)
        {
            if (view is IChildViewHost child)
            {
                invoke(child.CommitChildActivation);
            }
        }

        private static Exception CombineRebindErrors(List<Exception> errors) => errors.Count == 0 ? null :
                    errors.Count == 1 ? errors[0] : new AggregateException("View model rebind or cleanup failed.", errors);

        private static ValueTask<RebindOutcome> RejectedRebind(RebindRejection rejection) =>
                    new ValueTask<RebindOutcome>(new RebindOutcome(RebindStatus.Rejected, rejection));
    }
}
