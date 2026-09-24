namespace MUI
{
    /// <summary>可选渲染能力：在此 View 正下方设置宿主区域射线阻挡层。</summary>
    public interface IModalView : IView
    {
        void SetModalBarrier(bool enabled);
    }
}
