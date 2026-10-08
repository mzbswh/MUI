namespace MUI
{
    /// <summary>适配器自身管理释放责任时公开同一记录，避免外层为其建立重复的责任。</summary>
    public interface ICleanupResponsibilitySource
    {
        CleanupResponsibility CleanupResponsibility
        {
            get;
        }
    }
}
