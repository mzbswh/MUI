using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    public sealed class ViewLease : IViewLease
    {
        private readonly ResourceLease<IView> lease;

        public ViewLease(IView view, Func<IView, ValueTask> release, int? releaseThreadId = null)
        {
            lease = new ResourceLease<IView>(view, release, releaseThreadId);
        }

        public IView View => lease.Asset;

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
