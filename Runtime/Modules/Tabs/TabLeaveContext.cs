namespace MUI.Tabs
{
    /// <summary>离开守卫借用的源状态；取消后不能继续持有或操作 SourceModel。</summary>
    public sealed class TabLeaveContext
    {
        internal TabLeaveContext(string sourceKey, string targetKey, ViewModel sourceModel)
        {
            SourceKey = sourceKey;
            TargetKey = targetKey;
            SourceModel = sourceModel;
        }

        public string SourceKey
        {
            get;
        }

        public string TargetKey
        {
            get;
        }

        public ViewModel SourceModel
        {
            get;
        }
    }
}
