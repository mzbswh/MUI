using System;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewSlot
    {
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
