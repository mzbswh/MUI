using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>父子视图共用的参数事务。先公布完成信号，再执行外部回调，允许关闭安全等待收尾。</summary>
    internal sealed partial class ArgsUpdateOperation<TArgs>
    {
        private readonly IArgsUpdateHost<TArgs> host;
        private Func<CancellationToken, ValueTask> beforeCommit;
        private Action afterCommit;
        private readonly CancellationTokenSource cancellation;
        private readonly TaskCompletionSource<ArgsUpdateOutcome> completion =
            new TaskCompletionSource<ArgsUpdateOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        private Exception cancellationError;
        private bool started;

        internal ArgsUpdateOperation(IArgsUpdateHost<TArgs> host, CancellationToken token,
            Func<CancellationToken, ValueTask> beforeCommit = null, Action afterCommit = null)
        {
            this.host = host;
            this.beforeCommit = beforeCommit;
            this.afterCommit = afterCommit;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, host.ArgsLifetime.Token);
        }

        internal Task<ArgsUpdateOutcome> Completion => completion.Task;

        internal ValueTask<ArgsUpdateOutcome> Start(TArgs args)
        {
            if (started)
            {
                throw new InvalidOperationException("Argument update operation has already started.");
            }

            started = true;
            _ = RunAsync(args);
            return new ValueTask<ArgsUpdateOutcome>(completion.Task);
        }

        internal void Cancel()
        {
            if (completion.Task.IsCompleted)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (Exception error)
            {
                if (completion.Task.IsCompleted)
                {
                    // 取消内联结束更新后，后续取消回调仍可能失败，不能静默丢失。
                    UIErrors.Report(error);
                }
                else
                {
                    cancellationError = error;
                }
            }
        }

        private async Task RunAsync(TArgs args)
        {
            using var diagnostic = UIErrors.BeginPhase("ArgsPreparation");
            var failurePhase = "ArgsPreparation";
            var status = ArgsUpdateStatus.PreparationFailed;
            var errors = new List<Exception>();
            void AddFailure(Exception error)
            {
                UIErrors.AttachPhase(error, failurePhase);
                errors.Add(error);
            }
            var cleanup = ArgsUpdateCleanup.NotRequired;
            var commitStarted = false;
            var commitGateAcquired = false;
            var viewFaulted = false;
            IPreparedArgsUpdate candidate = null;
            IDisposable inputBlocker = null;
            try
            {
                RequireCurrent();
                host.InvokeArgsCallback(() => inputBlocker = host.ArgsInput.InputGate.Block("Updating view arguments"));
                RequireCurrent();
                await host.InvokeArgsCallbackAsync(async () =>
                {
                    candidate = await host.ArgsUpdater.PrepareArgsUpdateAsync(args, cancellation.Token);
                });
                // 返回即接管候选，即使准备忽略取消也不能把迟到结果遗失。
                RequireCurrent();
                if (candidate == null)
                {
                    throw new InvalidOperationException("Argument preparation returned no candidate.");
                }

                if (beforeCommit != null)
                {
                    await host.InvokeArgsCallbackAsync(() => beforeCommit(cancellation.Token));
                    commitGateAcquired = true;
                    RequireCurrent();
                }

                host.InvokeArgsCallback(() =>
                {
                    using var commitDiagnostic = UIErrors.BeginPhase("ArgsCommit");
                    failurePhase = "ArgsCommit";
                    commitStarted = true;
                    host.SetArgs(args);
                    candidate.Commit();
                    host.ArgsCommitted();
                });
                status = ArgsUpdateStatus.Applied;
            }
            catch (OperationCanceledException) when (!commitStarted &&
                (cancellation.IsCancellationRequested || !host.CanUpdateArgs))
            {
                status = ArgsUpdateStatus.CancelledBeforeCommit;
            }
            catch (Exception error)
            {
                AddFailure(error);
                status = commitStarted ? ArgsUpdateStatus.CommitFailed : ArgsUpdateStatus.PreparationFailed;
                // 提交可能已执行任意业务副作用，不能通过反向 setter 宣称恢复安全。
                viewFaulted = commitStarted;
            }
            finally
            {
                using var cleanupDiagnostic = UIErrors.BeginPhase("ArgsCleanup");
                failurePhase = "ArgsCleanup";
                if (commitGateAcquired)
                {
                    try
                    {
                        afterCommit?.Invoke();
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        cleanup = ArgsUpdateCleanup.Failed;
                        viewFaulted = true;
                    }
                }

                // 提交失败后立即撤销业务资格，候选异步释放期间也不能继续 Tick 或接受命令。
                var notifiedFailure = false;
                if (viewFaulted)
                {
                    try
                    {
                        host.ArgsFaulted(Combine(errors));
                        notifiedFailure = true;
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                    }
                }

                if (candidate != null)
                {
                    cleanup = ArgsUpdateCleanup.Complete;
                    try
                    {
                        await host.InvokeArgsCallbackAsync(() => CleanupRegistry.ReleaseAsync(candidate,
                            "ArgsUpdatePreparation", host.ArgsLifetime));
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        cleanup = ArgsUpdateCleanup.Failed;
                    }
                }

                if (inputBlocker != null)
                {
                    try
                    {
                        host.InvokeArgsCallback(inputBlocker.Dispose);
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        cleanup = ArgsUpdateCleanup.Failed;
                        viewFaulted = true;
                    }
                }

                if (cancellationError != null)
                {
                    AddFailure(cancellationError);
                    cleanup = ArgsUpdateCleanup.Failed;
                }

                var failure = Combine(errors);
                if (viewFaulted && !notifiedFailure)
                {
                    try
                    {
                        // 先标记实例不可复用，再唤醒已经等待的关闭或停用流程。
                        host.ArgsFaulted(failure);
                    }
                    catch (Exception error)
                    {
                        AddFailure(error);
                        failure = Combine(errors);
                    }
                }

                beforeCommit = null;
                afterCommit = null;
                cancellation.Dispose();
                var outcome = new ArgsUpdateOutcome(status, error: failure,
                    cleanup: cleanup, viewFaulted: viewFaulted);
                // 先保存诊断，再唤醒关闭或停用；不能依赖调用方的 await 延续先于关闭执行。
                host.RecordArgsUpdateOutcome(outcome);
                completion.TrySetResult(outcome);
            }
        }

        private void RequireCurrent()
        {
            host.RequireArgsThread();
            cancellation.Token.ThrowIfCancellationRequested();
            var alive = false;
            host.InvokeArgsCallback(() =>
            {
                var view = host.ArgsInput;
                alive = view != null && view.IsAlive;
            });
            cancellation.Token.ThrowIfCancellationRequested();
            if (!alive || !host.CanUpdateArgs)
            {
                throw new OperationCanceledException("View became unavailable before argument update commit.");
            }
        }

        private static Exception Combine(List<Exception> errors) => errors.Count == 0 ? null : errors.Count == 1
            ? errors[0] : new AggregateException("Argument update or cleanup failed.", errors);
    }
}
