namespace MUI.Resources
{
    /// <summary>
    /// 独立的同步加载与释放契约，项目实现无需提供异步方法。
    /// 返回凭证及失败回滚都必须同步完成，不能阻塞异步加载或启动后台清理。
    /// 若回滚本身失败且无法确认全部资源归还，抛出 SynchronousResourceLoadException，
    /// 使调用方能够报告未完成的清理；普通加载异常表示后端已完成部分构造清理。
    /// </summary>
    public interface ISynchronousResourceLoader
    {
        ISynchronousResourceLease<T> Load<T>(string key)
            where T : class;
    }
}
