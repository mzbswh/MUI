namespace MUI.Navigation
{
    public enum BackResponse
    {
        Close,
        Ignore,
        Handled
    }

    /// <summary>同步局部返回处理；确认属于关闭守卫流程。</summary>
    public interface IBackHandler
    {
        BackResponse HandleBack();
    }
}
