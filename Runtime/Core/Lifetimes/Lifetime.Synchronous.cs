using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace MUI
{
    public sealed partial class Lifetime
    {
        /// <summary>
        /// 当前是否具备同步释放条件，仅作能力提示；Dispose 会在锁内重新检查。
        /// 已完成的释放也返回 true，重复 Dispose 会重现原来的清理错误。
        /// </summary>
        public bool CanDisposeSynchronously
        {
            get
            {
                lock (gate)
                {
                    return disposalStarted ? disposed : CanBeginSynchronousDisposal();
                }
            }
        }

        /// <summary>
        /// 同步取消并逆序释放资源，逐项收集错误；不调用异步释放回调，也不等待任务。
        /// 存在异步资源、在途操作或正在执行的取消/释放时，在修改状态前拒绝。
        /// 清理回调不得重入自身释放；已经完成的释放可重复观察同一结果。
        /// </summary>
        public void Dispose()
        {
            for (var frame = CurrentRelease.Value; frame != null; frame = frame.Parent)
            {
                if (frame.Active && ReferenceEquals(frame.Owner, this))
                {
                    throw new InvalidOperationException("A cleanup callback cannot dispose the lifetime that is releasing it.");
                }
            }

            Action[] cleanup;
            TaskCompletionSource<bool> completion;
            bool cancel;
            lock (gate)
            {
                if (disposalStarted)
                {
                    if (!disposed)
                    {
                        throw new InvalidOperationException("Lifetime disposal is still in progress.");
                    }

                    ThrowDisposalFailure();
                    return;
                }

                if (!CanBeginSynchronousDisposal())
                {
                    throw new InvalidOperationException("Lifetime cannot be disposed synchronously; use DisposeAsync.");
                }

                cleanup = new Action[releases.Count];
                for (var i = 0; i < cleanup.Length; ++i)
                {
                    cleanup[i] = releases[i].Synchronous;
                }
                // 纯同步生命周期直接记录释放状态；允许异步等待的模式才需要完成信号。
                completion = null;
                if (Mode != LifetimeMode.Synchronous)
                {
                    completion = disposal = NewCompletion();
                }

                disposalStarted = true;
                cancel = !ended;
                // 原子停止准入并接管取消，防止另一线程在检查后启动工作或取消回调。
                ended = true;
            }

            if (cancel)
            {
                CompleteCancellation();
            }

            var errors = new List<Exception>();
            lock (gate)
            {
                errors.AddRange(cancellationErrors);
            }

            try
            {
                for (var i = cleanup.Length - 1; i >= 0; --i)
                {
                    var previous = CurrentRelease.Value;
                    var frame = new ReleaseFrame { Owner = this, Parent = previous };
                    CurrentRelease.Value = frame;
                    try
                    {
                        cleanup[i]();
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                    finally
                    {
                        frame.Active = false;
                        frame.Owner = null;
                        CurrentRelease.Value = previous;
                    }
                }
            }
            finally
            {
                CompleteDisposal(completion, errors);
            }

            ThrowDisposalFailure();
        }

        private bool CanBeginSynchronousDisposal()
        {
            if (synchronousOperations != 0 || operations.Count != 0 || (ended && !cancellationCompleted))
            {
                return false;
            }

            foreach (var release in releases)
            {
                if (release.Synchronous == null || (release.Resource != null && !release.Resource.CanDisposeSynchronously))
                {
                    return false;
                }
            }

            return true;
        }

        private void ThrowDisposalFailure()
        {
            if (disposalFailure != null)
            {
                ExceptionDispatchInfo.Capture(disposalFailure).Throw();
            }
        }

        private void RequireAsyncAllowed()
        {
            if (Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("A synchronous lifetime does not accept asynchronous work or cleanup.");
            }
        }

        private void RequireSynchronousChild(object resource)
        {
            if (ReferenceEquals(resource, this))
            {
                throw new ArgumentException("A lifetime cannot own itself.", nameof(resource));
            }

            // 在所有权转移前拒绝当前明确不支持同步释放的资源，
            // 例如异步模式的资源槽；不能等到父生命周期销毁时才发现无法归还。
            if (resource is ISynchronousDisposable synchronous && !synchronous.CanDisposeSynchronously)
            {
                throw new InvalidOperationException("Resource is not currently eligible for synchronous ownership.");
            }

            // 同步释放登记不能持有稍后还可登记异步工作的子生命周期。
            if (resource is Lifetime child && child.Mode != LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("Synchronous ownership requires a synchronous child lifetime.");
            }
        }
    }
}
