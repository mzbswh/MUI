namespace MUI
{
    /// <summary>宿主分配绝对区间；偏移 0 为遮罩，1 为根 Canvas，其余供内部内容使用。</summary>
    public interface IRenderOrderedView : IView
    {
        void SetRenderOrder(int sortingLayerId, int start, int span, int closingBarrierOrder);
    }
}
