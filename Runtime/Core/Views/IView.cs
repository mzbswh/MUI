namespace MUI
{
    /// <summary>渲染边界，宿主门控始终优先于局部可见性请求。</summary>
    public interface IView
    {
        /// <summary>渲染实例是否仍有效。</summary>
        bool IsAlive
        {
            get;
        }

        /// <summary>按名称和可赋值类型查询元素，缺失或歧义由实现报告契约错误。</summary>
        TElement GetElement<TElement>(string name)
            where TElement : class, IElement;

        /// <summary>设置宿主可见性和输入限制；局部状态不能绕过宿主限制。</summary>
        void SetHostState(bool visible, bool interactable);
    }
}
