namespace MUI
{
    /// <summary>控件可选的属性绑定约束，供运行时和编辑器共用；查询不能初始化或修改控件。</summary>
    public interface IBindingPropertyPolicy
    {
        /// <summary>
        /// 验证属性允许的绑定方向，并返回写入冲突检测使用的目标名称。
        /// 多个属性操作同一资源时返回相同名称；不允许的方向抛出异常。
        /// </summary>
        string GetBindingWriteTarget(string propertyName, BindingMode mode);
    }
}
