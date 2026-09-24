using System;

namespace MUI.Resources
{
    /// <summary>独立的资源驻留持有权，创建界面不会消费此凭证。</summary>
    public interface IPreloadLease : IAsyncDisposable
    {
        ViewResource Resource
        {
            get;
        }
    }
}
