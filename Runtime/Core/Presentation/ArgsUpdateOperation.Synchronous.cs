using System;
using System.Collections.Generic;

namespace MUI
{
    internal sealed partial class ArgsUpdateOperation<TArgs>
    {
        /// <summary>使用同一宿主资格、参数存储和结果协议直接执行同步事务。</summary>
        internal static ArgsUpdateOutcome RunSynchronous(IArgsUpdateHost<TArgs> host,
            ISynchronousArgsUpdatePresenter<TArgs> updater, TArgs nextArgs, Action endOperation, Action beforeCommit = null)
        {
            var status = ArgsUpdateStatus.PreparationFailed;
            var cleanup = ArgsUpdateCleanup.NotRequired;
            var errors = new List<Exception>();
            var previousArgs = host.Args;
            var commitStarted = false;
            var recoveryFailed = false;
            ISynchronousPreparedArgsUpdate candidate = null;
            IDisposable blocker = null;

            void RequireCurrent()
            {
                host.RequireArgsThread();
                host.ArgsLifetimeToken.ThrowIfCancellationRequested();
                var alive = false;
                host.InvokeArgsCallback(() =>
                {
                    var input = host.ArgsInput;
                    alive = input != null && input.IsAlive;
                });
                host.ArgsLifetimeToken.ThrowIfCancellationRequested();
                if (!alive || !host.CanUpdateArgs)
                {
                    throw new OperationCanceledException("View became unavailable before synchronous argument commit.");
                }
            }

            try
            {
                RequireCurrent();
                host.InvokeArgsCallback(() => blocker = host.ArgsInput.InputGate.Block("Updating view arguments"));
                RequireCurrent();
                host.InvokeArgsCallback(() => candidate = updater.PrepareArgsUpdate(nextArgs));
                // 先接管返回候选，复核失败也必须同步释放。
                RequireCurrent();
                if (candidate == null)
                {
                    throw new InvalidOperationException("Synchronous argument preparation returned no candidate.");
                }

                if (beforeCommit != null)
                {
                    host.InvokeArgsCallback(beforeCommit);
                    RequireCurrent();
                }

                host.InvokeArgsCallback(() =>
                {
                    commitStarted = true;
                    host.SetArgs(nextArgs);
                    candidate.Commit();
                    host.ArgsCommitted();
                });
                status = ArgsUpdateStatus.Applied;
            }
            catch (OperationCanceledException) when (!commitStarted &&
                (host.ArgsLifetimeToken.IsCancellationRequested || !host.CanUpdateArgs))
            {
                status = ArgsUpdateStatus.CancelledBeforeCommit;
            }
            catch (Exception failure)
            {
                errors.Add(failure);
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
                    catch (Exception rollback)
                    {
                        errors.Add(rollback);
                        recoveryFailed = true;
                    }
                }
            }
            finally
            {
                if (candidate != null)
                {
                    cleanup = ArgsUpdateCleanup.Complete;
                    try
                    {
                        host.InvokeArgsCallback(candidate.Dispose);
                    }
                    catch (Exception failure)
                    {
                        errors.Add(failure);
                        cleanup = ArgsUpdateCleanup.Failed;
                    }
                }

                // 候选不再使用宿主资源后撤销操作标志，允许在输入仍被屏蔽时同步故障关闭。
                endOperation();
            }

            var recoveryAttempted = false;
            var recoveryClosed = false;
            void CloseUnsafeHost()
            {
                recoveryAttempted = true;
                var failure = Combine(errors);
                host.RecordArgsUpdateOutcome(new ArgsUpdateOutcome(status, error: failure,
                    cleanup: cleanup, recoveryFailed: true));
                try
                {
                    host.ArgsRecoveryFailed(failure);
                    recoveryClosed = true;
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (recoveryFailed)
            {
                CloseUnsafeHost();
            }

            if (blocker != null && (!recoveryFailed || recoveryClosed))
            {
                try
                {
                    host.InvokeArgsCallback(blocker.Dispose);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                    cleanup = ArgsUpdateCleanup.Failed;
                    recoveryFailed = true;
                }
            }

            if (recoveryFailed && !recoveryAttempted)
            {
                CloseUnsafeHost();
            }

            var outcome = new ArgsUpdateOutcome(status, error: Combine(errors), cleanup: cleanup,
                recoveryFailed: recoveryFailed);
            host.RecordArgsUpdateOutcome(outcome);
            return outcome;
        }
    }
}
