namespace MUI.Resources
{
    /// <summary>
    /// 独立同步预加载能力。返回独立驻留持有权，不创建界面，不消费已有界面的持有权。
    /// 失败时必须在抛出前同步清理部分加载，不能留下后台清理任务。
    /// 回滚释放失败时抛出 SynchronousResourceLoadException，供导航保留失败占用。
    /// </summary>
    public interface ISynchronousPreloadViewProvider : ISynchronousViewProvider
    {
        ISynchronousPreloadLease Preload(ViewResource resource);
    }
}
