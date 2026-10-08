using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>已取得的视图及一次归还责任；重复释放观察同一清理结果。</summary>
    public sealed class AcquiredView : IAcquiredView
    {
        private readonly AcquiredResource<IView> resource;

        public AcquiredView(IView view, Func<IView, ValueTask> release, int? releaseThreadId = null)
        {
            resource = new AcquiredResource<IView>(view, release, releaseThreadId);
        }

        public IView View => resource.Asset;

        public ValueTask DisposeAsync() => resource.DisposeAsync();
    }
}
