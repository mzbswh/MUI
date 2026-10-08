using System;

namespace MUI.Resources
{
    /// <summary>
    /// 一份可独立释放的持有权，释放不等于卸载共享资源。
    /// 实现必须保证 DisposeAsync 幂等。
    /// </summary>
    public interface IAcquiredResource<out T> : IAsyncDisposable where T : class
    {
        T Asset
        {
            get;
        }
    }
}
