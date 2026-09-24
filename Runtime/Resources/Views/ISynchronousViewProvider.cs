namespace MUI.Resources
{
    /// <summary>
    /// 独立的同步视图资源契约，项目无需实现异步方法。
    /// 成功时交出隐藏视图，失败时同步释放部分构造资源；能力查询不触发加载。
    /// 回滚释放失败时抛出 SynchronousResourceLoadException，不能将残留资源视为已归还。
    /// </summary>
    public interface ISynchronousViewProvider
    {
        SyncCreateAvailability GetSyncAvailability(ViewResource resource);

        ISynchronousViewLease Create(ViewResource resource);
    }
}
