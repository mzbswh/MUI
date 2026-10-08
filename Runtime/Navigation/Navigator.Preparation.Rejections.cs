namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>共享准备拒绝在 Open 与 Replace 中保持同一业务含义。</summary>
        private static ReplaceRejection ToReplacementRejection(OpenRejection rejection)
        {
            switch (rejection)
            {
                case OpenRejection.ConflictingData:
                    return ReplaceRejection.ConflictingData;
                case OpenRejection.CleanupCapacity:
                    return ReplaceRejection.CleanupCapacity;
                case OpenRejection.DependencyCycle:
                    return ReplaceRejection.DependencyCycle;
                case OpenRejection.DependencyOrderConflict:
                    return ReplaceRejection.DependencyOrderConflict;
                case OpenRejection.DependencyLimit:
                    return ReplaceRejection.DependencyLimit;
                case OpenRejection.Busy:
                    return ReplaceRejection.Busy;
                default:
                    return ReplaceRejection.Superseded;
            }
        }
    }
}
