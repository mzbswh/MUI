namespace MUI.UGUI
{
    /// <summary>派发结果只表示交互是否被接受，不代表绑定命令执行成功或异步业务已完成。</summary>
    public enum UIAutomationStatus
    {
        Accepted,
        NotReady,
        InputBlocked,
        Reentrant
    }
}
