namespace MUI
{
    /// <summary>
    /// 可选的 UI 线程逐帧进入效果；采样必须同步，返回后
    /// 不能保留未结束工作，Finish 始终恢复稳定视觉状态。
    /// </summary>
    public interface IEnterTransitionView : IView
    {
        float EnterDuration
        {
            get;
        }

        void SampleEnter(float normalizedTime);

        void FinishEnter();
    }
}
