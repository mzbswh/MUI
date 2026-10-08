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

        internal bool IsRebinding => Rebinding != null && !Rebinding.IsCompleted;

        /// <summary>正常关闭守卫可取消换绑；取消不等于释放，关闭仍须等待 Rebinding。</summary>
        internal void CancelRebind()
        {
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
            Func<IView, TViewModel, BindingContext> bindingFactory, LifetimeScope activation,
            Action requireActive, Action committed, Action<Exception> viewFaulted, CancellationToken token,
            Func<CancellationToken, ValueTask> enterCommit = null, Action exitCommit = null) =>
            StartRebind(next, view, target, bindingFactory, activation, requireActive, committed,
                viewFaulted, token, enterCommit, exitCommit, null);

        private ValueTask<RebindOutcome> StartRebind(TViewModel next, IView view, ICommandTarget target,
            Func<IView, TViewModel, BindingContext> bindingFactory, LifetimeScope activation,
            Action requireActive, Action committed, Action<Exception> viewFaulted, CancellationToken token,
            Func<CancellationToken, ValueTask> enterCommit, Action exitCommit, RebindStage stage)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (IsRebinding)
            {
                return RejectRebind(RebindRejection.Busy, stage);
            }

            if (destroyed || !opened || Binding == null)
            {
                return RejectRebind(RebindRejection.SourceUnavailable, stage);
            }

            if (ReferenceEquals(next, model))
            {
                stage?.FailPreparation(new InvalidOperationException("View already uses the requested model."));
                return new ValueTask<RebindOutcome>(new RebindOutcome(RebindStatus.Applied));
            }

            if (Binding.IsExecutingCommand)
            {
                return RejectRebind(RebindRejection.Reentrant, stage);
            }

            if (!(view is IInputView input))
            {
                return RejectRebind(RebindRejection.InputControlUnsupported, stage);
            }

            if (!(Binding is IImmediateRebindDetach))
            {
                return RejectRebind(RebindRejection.BindingDetachUnsupported, stage);
            }

            if ((enterCommit == null) != (exitCommit == null))
            {
                throw new ArgumentException("A rebind commit must provide both queue callbacks.");
            }

            if (stage != null && enterCommit != null)
            {
                throw new ArgumentException("A staged child rebind cannot acquire a navigation commit queue.");
            }

            rebindCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, activation.Token);
            rebindCancellationError = null;
            var completion = new TaskCompletionSource<RebindOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            // 必须先发布信号；输入门控、解绑和业务钩子都可能同步请求关闭。
            Rebinding = completion.Task;
            _ = ExecuteRebindAsync(next, view, input, target, bindingFactory, activation,
                requireActive, committed, viewFaulted, rebindCancellation, completion, enterCommit, exitCommit, stage);
            return new ValueTask<RebindOutcome>(completion.Task);
        }

        /// <summary>子视图借用现有原生节点，候选准备与父绑定的同步提交分开驱动。</summary>
        internal async ValueTask<PreparedViewRebind> PrepareStagedRebindAsync(TViewModel next, IView view,
            ICommandTarget target, Func<IView, TViewModel, BindingContext> bindingFactory,
            LifetimeScope activation, Action requireActive, Action committed,
            Action<Exception> viewFaulted, CancellationToken token)
        {
            var stage = new RebindStage();
            _ = StartRebind(next, view, target, bindingFactory, activation, requireActive,
                committed, viewFaulted, token, null, null, stage);
            return await stage.Ready.Task;
        }

        private async Task ExecuteRebindAsync(TViewModel next, IView view, IInputView input,
                    ICommandTarget target, Func<IView, TViewModel, BindingContext> bindingFactory,
                    LifetimeScope activation, Action requireActive, Action committed, Action<Exception> viewFaulted,
                    CancellationTokenSource cancellation, TaskCompletionSource<RebindOutcome> completion,
                    Func<CancellationToken, ValueTask> enterCommit, Action exitCommit, RebindStage stage)
        {
            using var diagnostic = UIErrors.BeginPhase("RebindPreparation");
            var failurePhase = "RebindPreparation";
            var previousBinding = Binding;
            BindingContext preparedBinding = null;
            IPreparedBindingRebind preparedBindingCommit = null;
            IPreparedViewModelRebind preparedOperation = null;
            ValueTask previousCleanup = default;
            var detached = false;
            var detachStarted = false;
            var commitEntered = false;
            var viewFaultedState = false;
            var status = RebindStatus.Failed;
            var cleanup = RebindCleanup.NotRequired;
            var errors = new List<Exception>();
            void AddFailure(Exception error)
            {
                UIErrors.AttachPhase(error, failurePhase);
                errors.Add(error);
            }
            IDisposable blocker = null;

            async ValueTask EnterCommitAsync()
            {
                if (enterCommit != null)
                {
                    await enterCommit(cancellation.Token);
                    commitEntered = true;
                }
            }

            void ExitCommit()
            {
                if (commitEntered)
                {
                    commitEntered = false;
                    exitCommit();
                }
            }

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

            void CommitPreparedBinding()
            {
                using var commitDiagnostic = UIErrors.BeginPhase("RebindCommit");
                failurePhase = "RebindCommit";
                RequireCurrent();
                invoke(preparedBindingCommit.Validate);
                RequireCurrent();
                invoke(() => blocker = input.InputGate.Block("Rebinding view model"));
                RequireCurrent();
                invoke(preparedBindingCommit.BeginCommit);
                RequireCurrent();
                detachStarted = true;
                cleanup = RebindCleanup.Complete;
                invoke(() =>
                {
                    previousCleanup = ((IImmediateRebindDetach)previousBinding).BeginRebindDetach();
                    if (previousBinding.State == BindingState.Bound || previousBinding.State == BindingState.Binding)
                    {
                        throw new InvalidOperationException("Rebind detachment did not stop the previous binding synchronously.");
                    }
                });
                detached = true;
                RequireCurrent();

                // 旧订阅同步切断后才能交出新模型；命令清理在提交权释放后等待。
                modelOwnership.SetModel(next);
                Binding = preparedBinding;
                preparedBinding = null;
                invoke(() => presenter.ChangeViewModel(next));
                RequireCurrent();
                if (preparedOperation != null)
                {
                    invoke(preparedOperation.Commit);
                    RequireCurrent();
                }

                invoke(Binding.Bind);
                RequireCurrent();
                invoke(Binding.CommitSourceWrites);
                RequireCurrent();
                invoke(preparedBindingCommit.Commit);
                RequireCurrent();
                ExitCommit();
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
                if (presenter is IViewModelRebindPresenter<TViewModel> preparer)
                {
                    await invokeAsync(async () =>
                    {
                        preparedOperation = await preparer.PrepareViewModelRebindAsync(next, cancellation.Token);
                    });
                    RequireCurrent();
                    if (preparedOperation == null)
                    {
                        throw new InvalidOperationException("View model rebind preparation returned no candidate.");
                    }
                }

                // 工厂与身份校验失败不能影响当前绑定。通过校验后才接管候选，
                // 无效工厂结果可能属于其他实例，不由本次换绑擅自释放。
                invoke(() =>
                {
                    var candidate = bindingFactory(view, next);
                    ViewPreparation.ValidateBinding(candidate, view, next);
                    preparedBinding = candidate;
                });
                RequireCurrent();
                if (!(preparedBinding is IBindingRebindPreparation bindingPreparation))
                {
                    throw new InvalidOperationException("Candidate binding does not support isolated rebind preparation.");
                }

                invoke(() =>
                {
                    preparedBinding.SetCommandTarget(target);
                    preparedBinding.SetRebindCleanupOwner(activation);
                });
                await invokeAsync(async () =>
                {
                    preparedBindingCommit = await bindingPreparation.PrepareRebindAsync(cancellation.Token);
                });
                if (preparedBindingCommit == null)
                {
                    throw new InvalidOperationException("Candidate binding returned no rebind preparation.");
                }

                RequireCurrent();
                if (stage == null)
                {
                    await EnterCommitAsync();
                    CommitPreparedBinding();
                }
                else
                {
                    var prepared = new PreparedViewRebind(
                        () =>
                        {
                            RequireCurrent();
                            invoke(preparedBindingCommit.Validate);
                            RequireCurrent();
                        },
                        () =>
                        {
                            Exception commitError = null;
                            try
                            {
                                CommitPreparedBinding();
                            }
                            catch (Exception error)
                            {
                                commitError = error;
                            }

                            stage.CompleteCommit(commitError);
                            if (commitError != null)
                            {
                                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(commitError).Throw();
                            }
                        },
                        () =>
                        {
                            CancelRebind();
                            stage.Abort();
                        },
                        completion.Task);
                    stage.Ready.TrySetResult(prepared);
                    using (cancellation.Token.Register(stage.Abort))
                    {
                        var commitError = await stage.CommitCompletion.Task;
                        if (commitError != null)
                        {
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(commitError).Throw();
                        }
                    }
                }

                await CompleteRebindChildrenAsync(view, cancellation.Token);
                RequireCurrent();
                await EnterCommitAsync();
                RequireCurrent();
                CommitRebindChildren(view);
                RequireCurrent();
                committed();
                status = RebindStatus.Applied;
                ExitCommit();
            }
            catch (Exception error)
            {
                if ((detachStarted && !detached) || status == RebindStatus.Applied)
                {
                    cleanup = RebindCleanup.Failed;
                }

                // 提交前来源、布局或资格失效与令牌取消一样保留旧会话，通过取消结果返回。
                // 已开始解绑则无法恢复旧绑定，仍按故障关闭保留异常和清理责任。
                if (detachStarted || cleanup == RebindCleanup.Failed || !(error is OperationCanceledException))
                {
                    AddFailure(error);
                }

                if (status != RebindStatus.Applied)
                {
                    status = error is OperationCanceledException ? RebindStatus.Cancelled : RebindStatus.Failed;
                    // 一旦开始解绑，旧会话已失去完整性，不能重新绑定旧模型假装回滚成功。
                    // 当前 Binding（含半绑定候选）交由宿主故障关闭清理；不在这里丢弃引用。
                    viewFaultedState = detachStarted;
                }
            }

            finally
            {
                using var cleanupDiagnostic = UIErrors.BeginPhase("RebindCleanup");
                failurePhase = "RebindCleanup";
                // 提交失败时先关闭宿主，再解除临时门控，不能短暂暴露已经损坏的绑定。
                var notifiedFailure = false;
                void NotifyFailure()
                {
                    if (!viewFaultedState || notifiedFailure)
                    {
                        return;
                    }

                    try
                    {
                        viewFaulted(CombineRebindErrors(errors));
                        notifiedFailure = true;
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                    }
                }

                NotifyFailure();

                try
                {
                    ExitCommit();
                }
                catch (Exception error)
                {
                    AddFailure(error);
                    cleanup = RebindCleanup.Failed;
                    viewFaultedState = detachStarted;
                }

                NotifyFailure();

                if (detachStarted)
                {
                    try
                    {
                        await previousCleanup;
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        cleanup = RebindCleanup.Failed;
                        viewFaultedState = true;
                    }
                }

                NotifyFailure();

                if (status == RebindStatus.Applied)
                {
                    try
                    {
                        // 原命令排空后才释放工厂模型；释放失败不撤销已提交的新模型。
                        await invokeAsync(modelOwnership.ReleaseReplacedModelAsync);
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        cleanup = RebindCleanup.Failed;
                    }
                }

                if (preparedBinding != null)
                {
                    try
                    {
                        await invokeAsync(preparedBinding.UnbindAsync);
                        if (cleanup == RebindCleanup.NotRequired)
                        {
                            cleanup = RebindCleanup.Complete;
                        }
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        cleanup = RebindCleanup.Failed;
                    }
                    preparedBinding = null;
                }

                if (preparedBindingCommit != null)
                {
                    try
                    {
                        // 框架预览在内部准备失败时也登记责任；外部对象由统一归还入口登记。
                        await invokeAsync(() => CleanupRegistry.ReleaseAsync(preparedBindingCommit,
                            "BindingRebindPreparation", preparedBindingCommit is BindingPreview ? null : activation));
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        cleanup = RebindCleanup.Failed;
                    }
                }

                if (preparedOperation != null)
                {
                    try
                    {
                        await invokeAsync(() => CleanupRegistry.ReleaseAsync(preparedOperation,
                            "ViewModelRebindPreparation", activation));
                        if (cleanup == RebindCleanup.NotRequired)
                        {
                            cleanup = RebindCleanup.Complete;
                        }
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        cleanup = RebindCleanup.Failed;
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
                        AddFailure(error);
                        viewFaultedState = true;
                        cleanup = RebindCleanup.Failed;
                    }
                }

                if (rebindCancellationError != null)
                {
                    AddFailure(rebindCancellationError);
                    cleanup = RebindCleanup.Failed;
                }

                NotifyFailure();
                var failure = CombineRebindErrors(errors);

                if (stage != null)
                {
                    stage.FailPreparation(failure ?? new OperationCanceledException(
                        "Staged view rebind ended before its candidate was ready."));
                }

                cancellation.Dispose();
                rebindCancellation = null;
                rebindCancellationError = null;
                if (cleanup == RebindCleanup.Failed && rebindCleanupFailure == null)
                {
                    // 后续成功换绑不能抹掉已发生的资源清理失败；只保留首个失败，避免无限历史。
                    rebindCleanupFailure = failure;
                }

                var outcome = new RebindOutcome(status, error: failure, viewFaulted: viewFaultedState, cleanup: cleanup);
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

        private static ValueTask<RebindOutcome> RejectRebind(RebindRejection rejection, RebindStage stage)
        {
            stage?.FailPreparation(new InvalidOperationException($"Staged view rebind was rejected: {rejection}."));
            return RejectedRebind(rejection);
        }

        private sealed class RebindStage
        {
            internal readonly TaskCompletionSource<PreparedViewRebind> Ready =
                new TaskCompletionSource<PreparedViewRebind>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly TaskCompletionSource<Exception> CommitCompletion =
                // 父提交已在主线程进行；立即完成的子收尾沿当前调用链继续，不能人为延后一帧。
                new TaskCompletionSource<Exception>();

            internal void FailPreparation(Exception error) => Ready.TrySetException(error);

            internal void CompleteCommit(Exception error) => CommitCompletion.TrySetResult(error);

            internal void Abort() => CommitCompletion.TrySetResult(
                new OperationCanceledException("Staged view rebind was abandoned."));
        }
    }
}
