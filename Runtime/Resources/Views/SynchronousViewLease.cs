using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>同步视图持有权；异步接口仅用于兼容现有宿主，释放始终执行同一个同步回调。</summary>
    public sealed class SynchronousViewLease : ISynchronousViewLease, IViewLease
    {
        private readonly SynchronousResourceLease<IView> lease;

        public SynchronousViewLease(IView view, Action<IView> release, int? releaseThreadId = null)
        {
            lease = new SynchronousResourceLease<IView>(view, release, releaseThreadId);
        }

        public IView View => lease.Asset;

        public void Dispose() => lease.Dispose();

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
