namespace MUI.Samples.Navigation
{
    /// <summary>分页达到总容量时的处理；滑动窗口只保留当前加载方向最近取得的条目。</summary>
    public enum PageOverflowPolicy
    {
        /// <summary>默认策略；请求缩小至剩余容量，满容量后拒绝继续加载。</summary>
        Reject,

        /// <summary>继续请求整页；追加时淘汰头部，前插时淘汰尾部，不自动重新加载已淘汰内容。</summary>
        EvictOppositeEnd
    }
}
