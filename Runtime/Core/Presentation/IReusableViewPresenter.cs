namespace MUI
{
    /// <summary>
    /// 显式声明可缓存复用：OnOpen 必须按新参数重置激活状态，OnClose 清理本次激活。
    /// 不得保存旧 ActivationContext 用于新激活；实例资源可保留到 OnDestroy。
    /// </summary>
    public interface IReusableViewPresenter
    {
    }
}
