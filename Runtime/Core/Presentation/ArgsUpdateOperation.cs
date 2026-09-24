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
        private Action beforeCommit;
        private readonly CancellationTokenSource cancellation;
        private readonly TaskCompletionSource<ArgsUpdateOutcome> completion =
            new TaskCompletionSource<ArgsUpdateOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        private Exception cancellationError;
        private bool started;

        internal ArgsUpdateOperation(IArgsUpdateHost<TArgs> host, CancellationToken token, Action beforeCommit = null)
        {
            this.host = host;
            this.beforeCommit = beforeCommit;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, host.ArgsLifetimeToken);
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
            var status = ArgsUpdateStatus.PreparationFailed;
            var errors = new List<Exception>();
            var cleanup = ArgsUpdateCleanup.NotRequired;
            var commitStarted = false;
            var recoveryFailed = false;
            var previousArgs = host.Args;
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
                    host.InvokeArgsCallback(beforeCommit);
                    RequireCurrent();
                }

                host.InvokeArgsCallback(() =>
                {
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
                errors.Add(error);
                status = commitStarted ? ArgsUpdateStatus.CommitFailed : ArgsUpdateStatus.PreparationFailed;
                if (commitStarted)
                {
                    try
                    {
                        host.InvokeArgsCallback(() =>
                        {
                            host.SetArgs(previousArgs);
                            candidate.Rollback();
                        });
                    }
                    catch (Exception rollbackError)
                    {
                        errors.Add(rollbackError);
                        recoveryFailed = true;
                    }
                }
            }
            finally
            {
                // 回滚失败后立即撤销业务资格，候选异步释放期间也不能继续 Tick 或接受命令。
                var notifiedFailure = false;
                if (recoveryFailed)
                {
                    try
                    {
                        host.ArgsRecoveryFailed(Combine(errors));
                        notifiedFailure = true;
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }

                if (candidate != null)
                {
                    cleanup = ArgsUpdateCleanup.Complete;
                    try
                    {
                        await host.InvokeArgsCallbackAsync(candidate.DisposeAsync);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
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
                        errors.Add(error);
                        cleanup = ArgsUpdateCleanup.Failed;
                        recoveryFailed = true;
                    }
                }

                if (cancellationError != null)
                {
                    errors.Add(cancellationError);
                    cleanup = ArgsUpdateCleanup.Failed;
                }

                var failure = Combine(errors);
                if (recoveryFailed && !notifiedFailure)
                {
                    try
                    {
                        // 先标记实例不可复用，再唤醒已经等待的关闭或停用流程。
                        host.ArgsRecoveryFailed(failure);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                        failure = Combine(errors);
                    }
                }

                beforeCommit = null;
                cancellation.Dispose();
                var outcome = new ArgsUpdateOutcome(status, error: failure,
                    cleanup: cleanup, recoveryFailed: recoveryFailed);
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
