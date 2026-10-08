using System;

namespace MUI.Resources
{
    public interface IAcquiredView : IAsyncDisposable
    {
        IView View
        {
            get;
        }
    }
}
