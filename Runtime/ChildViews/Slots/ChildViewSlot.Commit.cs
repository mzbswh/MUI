using System;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewSlot
    {
        /// <summary>验证外部已准备候选能在当前帧同步接管本槽。</summary>
        public void ValidatePreparedCandidate(ChildViewHandle candidate)
        {
            scope.RequireActive();
            if (stopped || committing || accepting || active != null || pending != null ||
                (clearing != null && !clearing.IsCompleted))
            {
                throw new InvalidOperationException("Child view slot has an unfinished change.");
            }

            if (candidate != null && (!candidate.BelongsTo(scope) || candidate.State != ChildViewState.Prepared))
            {
                throw new InvalidOperationException("Candidate must be prepared in this child view scope.");
            }
        }

        /// <summary>提交已隐藏准备的候选；返回旧内容退役的完成任务。</summary>
        public Task CommitPreparedCandidate(ChildViewHandle candidate)
        {
            ValidatePreparedCandidate(candidate);
            var previous = CommitCandidate(candidate, () => ValidatePreparedCandidateDuringCommit(candidate), null);
            if (previous == null || ReferenceEquals(previous, candidate))
            {
                return Task.CompletedTask;
            }

            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            clearing = finished.Task;
            _ = FinishClearingAsync(previous, finished);
            return clearing;
        }

        private void ValidatePreparedCandidateDuringCommit(ChildViewHandle candidate)
        {
            scope.RequireActive();
            if (stopped || (candidate != null && (!candidate.BelongsTo(scope) ||
                (candidate.State != ChildViewState.Prepared && candidate.State != ChildViewState.Active))))
            {
                throw new InvalidOperationException("Prepared child view changed during commit.");
            }
        }

        /// <summary>两种模式共用候选提交、旧画面隐藏、当前项通知及失败恢复顺序。</summary>
        private ChildViewHandle CommitCandidate(ChildViewHandle candidate, Action validate,
            Action<ChildViewHandle> committedCallback)
        {
            var previous = current;
            var previousVisible = previous != null && previous.LocalVisible;
            var previousInteractable = previous != null && previous.LocalInteractable;
            try
            {
                committing = true;
                if (candidate != null)
                {
                    candidate.SetLocalState(false, false);
                    validate();
                    candidate.Commit();
                }

                if (previous != null && previous.IsActive && !ReferenceEquals(previous, candidate))
                {
                    previous.SetLocalState(false, false);
                }

                if (previous != null && previous.State == ChildViewState.Retained && !ReferenceEquals(previous, candidate))
                {
                    previous.EndRetainedDisplay();
                }

                if (candidate != null)
                {
                    validate();
                    candidate.SetLocalState(true, true);
                }

                SetCurrent(candidate);
                committedCallback?.Invoke(current);
                return previous;
            }
            catch
            {
                SetCurrent(previous);
                if (previous != null && previous.IsActive)
                {
                    previous.SetLocalState(previousVisible, previousInteractable);
                }

                throw;
            }
            finally
            {
                committing = false;
            }
        }
    }
}
