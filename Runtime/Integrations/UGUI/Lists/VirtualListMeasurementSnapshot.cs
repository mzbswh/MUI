namespace MUI.UGUI
{
    /// <summary>最近一次列表维护帧的测量尝试次数；失败及无效尺寸回退也计入预算。</summary>
    public readonly struct VirtualListMeasurementSnapshot
    {
        internal VirtualListMeasurementSnapshot(int frame, int attempts, long totalAttempts)
        {
            Frame = frame;
            Attempts = attempts;
            TotalAttempts = totalAttempts;
        }

        public int Frame
        {
            get;
        }

        public int Attempts
        {
            get;
        }

        /// <summary>此 Element 创建以来的累计次数，达到 long.MaxValue 后保持饱和。</summary>
        public long TotalAttempts
        {
            get;
        }
    }
}
