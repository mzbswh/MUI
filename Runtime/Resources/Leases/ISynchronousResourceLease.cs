using System;

namespace MUI.Resources
{
    /// <summary>可同步释放的一份持有权；释放幂等，不能启动后台清理或阻塞等待异步任务。</summary>
    public interface ISynchronousResourceLease<out T> : IDisposable where T : class
    {
        T Asset
        {
            get;
        }
    }
}
