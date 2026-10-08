namespace MUI
{
    /// <summary>可选 Presenter 能力，每个非缩放宿主帧驱动一次。</summary>
    public interface IViewTick
    {
        void OnViewTick(float unscaledDeltaTime);
    }

    /// <summary>可选的逐帧 Tick 替代方案，间隔在实例创建时记录。</summary>
    public interface ILowFrequencyViewTick
    {
        float TickInterval
        {
            get;
        }

        void OnLowFrequencyTick(float intervalSeconds);
    }
}
