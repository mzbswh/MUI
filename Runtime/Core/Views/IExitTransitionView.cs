namespace MUI
{
    /// <summary>保留退役画面后的同步逐帧退出效果；不允许采样留下未结束的异步工作。</summary>
    public interface IExitTransitionView : IVisualRetentionView
    {
        float ExitDuration
        {
            get;
        }

        /// <summary>宿主覆盖关系可隐藏保留画面，但不能重新激活业务或输入。</summary>
        void SetExitVisible(bool visible);

        void SampleExit(float normalizedTime);

        /// <summary>恢复转场驱动器的稳定状态；宿主随后隐藏画面并结束保留。</summary>
        void FinishExit();
    }
}
