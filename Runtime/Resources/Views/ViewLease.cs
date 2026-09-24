using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    public sealed class ViewLease : IViewLease
    {
        private readonly ResourceLease<IView> lease;

        public ViewLease(IView view, Func<IView, ValueTask> release)
        {
            lease = new ResourceLease<IView>(view, release);
        }

        public IView View => lease.Asset;

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
