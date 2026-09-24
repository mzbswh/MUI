namespace MUI.UGUI
{
    /// <summary>列表实例化状态，独立于父级可见性和远程数据获取。</summary>
    public enum VirtualListStatus
    {
        Inactive,
        Empty,
        Loading,
        Ready,
        Error
    }
}
