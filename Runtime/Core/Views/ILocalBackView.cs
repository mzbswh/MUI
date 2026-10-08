namespace MUI
{
    /// <summary>在页面返回策略之前同步处理局部临时状态；返回 true 表示已消费本次输入。</summary>
    public interface ILocalBackView
    {
        bool TryHandleLocalBack();
    }
}
