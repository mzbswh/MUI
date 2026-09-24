using MUI.Resources;

namespace MUI.Samples.ResourceIntegration
{
    /// <summary>
    /// 项目加载适配示例的后端约束：同步加载可实例化资源，并允许在克隆仍存活时归还源资源持有权。
    /// 归还不能卸载或破坏克隆依赖，也不能启动后台等待；加载失败必须同步回滚。
    /// 不满足此所有权契约的后端应使用异步视图适配，不得仅因支持 Load 而声明此能力。
    /// </summary>
    public interface ISynchronousInstantiableResourceLoader : ISynchronousResourceLoader
    {
    }
}
