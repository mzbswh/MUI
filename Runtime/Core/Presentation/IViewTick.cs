namespace MUI
{
    /// <summary>可选 Presenter 能力，每个非缩放宿主帧驱动一次。</summary>
    public interface IViewTick
    {
        void OnViewTick(float unscaledDeltaTime);
    }

    /// <summary>可选的逐帧 Tick 替代方案，间隔在实例创建时记录。默认每帧最多调用一次，超期次数丢弃。</summary>
    public interface ILowFrequencyViewTick
    {
        float TickInterval
        {
            get;
        }

        void OnLowFrequencyTick(float intervalSeconds);
    }
    /// <summary>低频 Tick 的可选补执行能力。配置在实例创建时读取，页面与子视图使用相同规则。</summary>
    public interface ILowFrequencyViewTickCatchUp : ILowFrequencyViewTick
    {
        /// <summary>单帧最多执行次数，范围 1～32。超限次数丢弃，仅保留不足一个间隔的余量。</summary>
        int MaxTickCatchUp
        {
            get;
        }
    }
}
