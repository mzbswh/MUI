namespace MUI.Resources
{
    /// <summary>
    /// 可选的资源内容代际契约。内容失效时换成新的非空对象，未变化时始终返回同一对象。
    /// 按引用身份比较，不复用旧令牌；读取必须快速、同步且不改变资源状态。
    /// 提供方仍负责拒绝旧驻留资源与旧加载结果；导航据此停止复用旧实例和预加载记录。
    /// </summary>
    public interface IVersionedViewProvider : IViewProvider, IViewContentVersion
    {
    }
}
