namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        private static bool RequiresOverflowReplacement<TResult>(Route route, OpenOutcome<TResult> outcome) => outcome.Status == OpenStatus.Rejected && outcome.Rejection == OpenRejection.InstanceLimit && route.Policy.Overflow == OverflowPolicy.CloseOldest;

        private ViewInstance OldestOpenInstance(Route route)
        {
            ViewInstance oldest = null;
            foreach (var instance in entries.Values)
            {
                if (!ReferenceEquals(instance.Route, route) || instance.State != ViewState.Open)
                {
                    continue;
                }

                // 句柄 ID 表示创建顺序，焦点变化与置顶不能改变淘汰顺序。
                if (oldest == null || instance.Handle.Id < oldest.Handle.Id)
                {
                    oldest = instance;
                }
            }

            return oldest;
        }

        private static OpenOutcome<TResult> FromOverflowReplacement<TResult>(ViewHandle source, ReplaceOutcome<TResult> replacement)
        {
            var destination = replacement.Destination;
            if (replacement.IsCommitted)
            {
                return new OpenOutcome<TResult>(destination.Status, destination.Handle, destination.Rejection, destination.Error, destination.Cleanup, source, replacement.SourceCleanup);
            }

            if (replacement.Status != ReplaceStatus.Rejected)
            {
                return destination;
            }

            OpenRejection rejection;
            switch (replacement.Rejection)
            {
                case ReplaceRejection.Busy:
                    rejection = OpenRejection.Busy;
                    break;
                case ReplaceRejection.Reentrant:
                    rejection = OpenRejection.Reentrant;
                    break;
                case ReplaceRejection.InstanceLimit:
                    rejection = OpenRejection.InstanceLimit;
                    break;
                case ReplaceRejection.CleanupCapacity:
                    rejection = OpenRejection.CleanupCapacity;
                    break;
                case ReplaceRejection.CloseDenied:
                    rejection = OpenRejection.CloseDenied;
                    break;
                case ReplaceRejection.ConfirmationUnavailable:
                    rejection = OpenRejection.ConfirmationUnavailable;
                    break;
                case ReplaceRejection.CloseDecisionTimedOut:
                    rejection = OpenRejection.CloseDecisionTimedOut;
                    break;
                case ReplaceRejection.ConflictingData:
                    rejection = OpenRejection.ConflictingData;
                    break;
                case ReplaceRejection.DependencyCycle:
                    rejection = OpenRejection.DependencyCycle;
                    break;
                case ReplaceRejection.DependencyOrderConflict:
                    rejection = OpenRejection.DependencyOrderConflict;
                    break;
                case ReplaceRejection.DependencyLimit:
                    rejection = OpenRejection.DependencyLimit;
                    break;
                default:
                    rejection = OpenRejection.Superseded;
                    break;
            }

            return new OpenOutcome<TResult>(OpenStatus.Rejected, rejection: rejection, error: destination.Error, cleanup: destination.Cleanup);
        }
    }
}
