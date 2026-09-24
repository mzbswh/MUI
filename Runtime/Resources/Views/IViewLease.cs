using System;

namespace MUI.Resources
{
    public interface IViewLease : IAsyncDisposable
    {
        IView View
        {
            get;
        }
    }
}
