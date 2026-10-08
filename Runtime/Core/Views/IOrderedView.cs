namespace MUI
{
    /// <summary>可选渲染排序契约，调用按从后到前顺序执行。</summary>
    public interface IOrderedView : IView
    {
        void MoveToFront();
    }
}
