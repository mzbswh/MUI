using System;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewScope
    {
        private void ValidateTransition(ChildViewHandle handle, ChildViewState first, ChildViewState second)
        {
            RequireActive();
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (!handle.BelongsTo(this) || (handle.State != first && handle.State != second)
                || handle.IsExecuting || HasVisualRetention)
            {
                throw new InvalidOperationException("Child transition requires this scope's idle handle in a compatible state.");
            }

            foreach (var sibling in handles)
            {
                if (sibling.IsLifecycleExecuting)
                {
                    throw new InvalidOperationException("Cannot transition a child view from a sibling lifecycle callback.");
                }
            }
        }
    }
}
