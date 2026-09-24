using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewSlot
    {
        private readonly Func<ChildViewHandle, bool> retainRetiredSynchronous;
        private bool synchronousChanging;
        private bool synchronousCleanupFailed;
        private bool synchronousDisposalStarted;
        private bool synchronousDisposalCompleted;

        public LifetimeMode Mode => scope.Mode;

        public bool CanDisposeSynchronously
        {
            get
            {
                scope.RequireThread();
                if (Mode != LifetimeMode.Synchronous)
                {
                    return disposal != null && disposal.Task.IsCompleted;
                }

                if (synchronousDisposalStarted)
                {
                    return synchronousDisposalCompleted;
                }

                // 同步模式不会启动排队、异步清空或排空任务，只检查直接执行状态。
                return !IsExecuting && active == null && pending == null && drain == null
                    && retiring.Count == 0 && quarantined.Count == 0
                    && (current == null || current.CanCloseSynchronously);
            }
        }

        /// <summary>在调用方修改选择意图前检查，防止重入拒绝后留下未执行的新配置。</summary>
        public void RequireSynchronousIdle()
        {
            scope.RequireThread();
            if (Mode != LifetimeMode.Synchronous || !CanDisposeSynchronously || synchronousCleanupFailed)
            {
                throw new InvalidOperationException("Slot requires an idle synchronous lifecycle without failed cleanup.");
            }
        }

        /// <summary>
        /// 同步准备并提交候选，再完成旧内容清理。准备失败保留旧内容。
        /// Ready/Empty 且 Error 非空表示提交已发生，但旧资源清理失败，不能视为清理成功。
        /// </summary>
        public ChildViewChangeResult Replace(Func<ChildViewScope, ChildViewHandle> prepare,
            Action<ChildViewHandle> committed = null, Action validateBeforeDisplay = null)
        {
            if (prepare == null)
            {
                throw new ArgumentNullException(nameof(prepare));
            }

            return ChangeSynchronous(prepare, committed, validateBeforeDisplay);
        }

        /// <summary>同步撤下并释放当前内容；返回时不会遗留本槽启动的后台清理。</summary>
        public ChildViewChangeResult Clear() => ChangeSynchronous(null, null, null);

        private ChildViewChangeResult ChangeSynchronous(Func<ChildViewScope, ChildViewHandle> prepare,
            Action<ChildViewHandle> committed, Action validate)
        {
            RequireSynchronousIdle();
            if (stopped || !scope.IsActive)
            {
                return new ChildViewChangeResult(ChildViewChangeStatus.ParentInactive);
            }

            synchronousChanging = true;
            try
            {
                return scope.RunSynchronous(() => ApplySynchronous(prepare, committed, validate));
            }
            finally
            {
                synchronousChanging = false;
            }
        }

        private ChildViewChangeResult ApplySynchronous(Func<ChildViewScope, ChildViewHandle> prepare,
            Action<ChildViewHandle> committed, Action validate)
        {
            ChildViewHandle candidate = null;
            try
            {
                if (prepare != null)
                {
                    var previousFrame = preparationFrame.Value;
                    var frame = new PreparationFrame();
                    preparationFrame.Value = frame;
                    ChildViewHandle prepared;
                    try
                    {
                        prepared = prepare(scope);
                    }
                    finally
                    {
                        frame.Active = false;
                        preparationFrame.Value = previousFrame;
                    }

                    if (prepared == null || !prepared.BelongsTo(scope) || prepared.State != ChildViewState.Prepared)
                    {
                        throw new InvalidOperationException("Synchronous slot factories must return a Prepared handle from their supplied scope.");
                    }

                    candidate = prepared;
                }

                void Validate()
                {
                    validate?.Invoke();
                    if (stopped || !scope.IsActive)
                    {
                        throw new OperationCanceledException("Child slot host became inactive during synchronous preparation.");
                    }
                }

                Validate();
                var previous = CommitCandidate(candidate, Validate, committed);
                candidate = null;
                var status = current == null ? ChildViewChangeStatus.Empty : ChildViewChangeStatus.Ready;
                var cleanup = previous != null && !ReferenceEquals(previous, current) ? RetireSynchronous(previous, allowRetention: true) : null;
                return new ChildViewChangeResult(status, cleanup);
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                if (failure is ChildViewPreparationException)
                {
                    // 同步准备只包装清理失败；未知异步清理同样禁止复用，不查询任务推断安全性。
                    synchronousCleanupFailed = true;
                    cleanupErrors.Add(failure);
                }

                if (candidate != null)
                {
                    var cleanup = RetireSynchronous(candidate);
                    if (cleanup != null)
                    {
                        errors.Add(cleanup);
                    }
                }

                var error = errors.Count == 1 ? failure : new AggregateException("Synchronous slot change and cleanup failed.", errors);
                var status = !scope.IsActive || stopped ? ChildViewChangeStatus.ParentInactive
                    : failure is OperationCanceledException ? ChildViewChangeStatus.Cancelled : ChildViewChangeStatus.Failed;
                return new ChildViewChangeResult(status, error);
            }
        }

        private Exception RetireSynchronous(ChildViewHandle handle, bool allowRetention = false)
        {
            retiring.Add(handle);
            Exception failure = null;
            try
            {
                if (allowRetention && retainRetiredSynchronous != null)
                {
                    try
                    {
                        if (retainRetiredSynchronous(handle))
                        {
                            return null;
                        }
                    }
                    catch (Exception error)
                    {
                        failure = error;
                    }
                }

                try
                {
                    handle.Dispose();
                }
                catch (Exception error)
                {
                    failure = failure == null ? error : new AggregateException(failure, error);
                }

                if (failure != null)
                {
                    synchronousCleanupFailed = true;
                    cleanupErrors.Add(failure);
                }

                return failure;
            }
            finally
            {
                retiring.Remove(handle);
            }
        }

        public void Dispose()
        {
            scope.RequireThread();
            if (!CanDisposeSynchronously)
            {
                throw new InvalidOperationException("Slot cannot dispose synchronously while work or callbacks are in progress.");
            }

            if (Mode == LifetimeMode.Synchronous && !synchronousDisposalStarted)
            {
                synchronousDisposalStarted = true;
                stopped = true;
                var previous = current;
                try
                {
                    SetCurrent(null);
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }
                finally
                {
                    if (previous != null)
                    {
                        RetireSynchronous(previous);
                    }

                    CurrentChanged = null;
                    disposalFailure = cleanupErrors.Count == 0 ? null : new AggregateException("Synchronous child slot cleanup failed.", cleanupErrors);
                    synchronousDisposalCompleted = true;
                }
            }

            if (disposalFailure != null)
            {
                ExceptionDispatchInfo.Capture(disposalFailure).Throw();
            }
        }
    }
}
