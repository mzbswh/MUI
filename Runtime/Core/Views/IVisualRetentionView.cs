namespace MUI
{
    /// <summary>
    /// 保留当前显示门控并立即禁用输入。调用者仍须停止绑定与业务工作，
    /// 并将实例和显示资源保留到 EndVisualRetention 之后；不复制渲染内容。
    /// </summary>
    public interface IVisualRetentionView : IView
    {
        /// <summary>在取消激活之前调用；递归保留已经提交的子视图。</summary>
        void BeginVisualRetention();

        /// <summary>结束保留并重新应用最新门控；重复调用不产生额外效果。</summary>
        void EndVisualRetention();
    }
}
