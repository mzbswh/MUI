namespace MUI
{
    /// <summary>树节点的结构描述；身份由 TreeList 的条目键选择器提供，模型仍由业务方拥有。</summary>
    public sealed class TreeNode<T>
    {
        public TreeNode(T item, object parentKey = null, bool initiallyExpanded = false)
        {
            Item = item;
            ParentKey = parentKey;
            InitiallyExpanded = initiallyExpanded;
        }

        public T Item
        {
            get;
        }

        /// <summary>父节点的稳定条目键；null 表示根节点。父节点可以位于输入序列的后面。</summary>
        public object ParentKey
        {
            get;
        }

        public bool InitiallyExpanded
        {
            get;
        }
    }
}
