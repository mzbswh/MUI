namespace MUI
{
    /// <summary>
    /// 永久停止本绑定会话的数据流和命令，但保留当前 Element 值。
    /// 仍需执行 Unbind；这不是资源或视觉快照。
    /// </summary>
    public interface IFreezableBindingContext
    {
        void Freeze();
    }
}
