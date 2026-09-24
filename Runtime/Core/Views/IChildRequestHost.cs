namespace MUI
{
    /// <summary>
    /// 独立于可见性和业务 Tick 的子界面请求派发，由宿主在回调之外驱动。
    /// 待处理请求必须计入 HasChildTicks，并通过 ChildTickActivityChanged 通知宿主注册维护。
    /// </summary>
    public interface IChildRequestHost : IChildTickHost
    {
        void PumpChildRequests();
    }
}
