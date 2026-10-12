namespace MUI
{
    /// <summary>两种时间仅累计可更新帧；增量是上次回调以来的实际时长，累计值在每次激活时重置。</summary>
    public readonly struct UpdateTime
    {
        public double DeltaTime
        {
            get;
        }
        public double UnscaledDeltaTime
        {
            get;
        }
        public double ElapsedTime
        {
            get;
        }
        public double UnscaledElapsedTime
        {
            get;
        }

        internal UpdateTime(double deltaTime, double unscaledDeltaTime, double elapsedTime, double unscaledElapsedTime)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            ElapsedTime = elapsedTime;
            UnscaledElapsedTime = unscaledElapsedTime;
        }
    }
}
