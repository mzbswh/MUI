namespace MUI.Tabs
{
    /// <summary>单个 Tab 的展示信息；键和标题固定，可用性由控制器维护。</summary>
    public sealed class TabItemState
    {
        internal TabItemState(string key, string label, bool enabled)
        {
            Key = key;
            Label = label;
            Enabled = enabled;
        }

        /// <summary>目录内唯一的稳定标识，不使用展示标题作为身份。</summary>
        public string Key
        {
            get;
        }

        /// <summary>展示标题。</summary>
        public string Label
        {
            get;
        }

        /// <summary>是否允许选择。变化通过 TabViewModel.Items 属性通知发布。</summary>
        public bool Enabled
        {
            get; internal set;
        }
    }
}
