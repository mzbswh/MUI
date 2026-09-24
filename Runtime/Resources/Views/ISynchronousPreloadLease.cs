using System;

namespace MUI.Resources
{
    /// <summary>可同步释放的独立驻留持有权；释放不能等待界面销毁或异步卸载。</summary>
    public interface ISynchronousPreloadLease : IDisposable
    {
        ViewResource Resource
        {
            get;
        }
    }
}
