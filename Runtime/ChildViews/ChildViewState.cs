namespace MUI.ChildViews
{
    public enum ChildViewState
    {
        Preparing,
        Prepared,
        Active,
        Closing,
        Closed,
        Failed,
        /// <summary>业务已停用，画面与实例资源暂时保留；不代表在途任务已排空。</summary>
        Retained,
        /// <summary>正在排空旧激活；实例不可交互，也不可重新激活。</summary>
        Deactivating,
        /// <summary>旧激活已完整清理，实例隐藏且可准备新激活。</summary>
        Inactive
    }
}
