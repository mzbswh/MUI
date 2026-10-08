namespace MUI
{
    /// <summary>
    /// 投影提供的激活专属目标，不存入共享 ViewModel。
    /// 请求将关闭或完成操作入队，不等待发出该请求的命令。
    /// 实现按类型化激活契约校验 TResult。
    /// </summary>
    public interface ICommandTarget
    {
        bool IsActive
        {
            get;
        }

        void RequestClose();

        void Complete<TResult>(TResult result);
    }
}
