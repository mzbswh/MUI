using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    public enum SyncCreateAvailability
    {
        Available,
        RequiresPreload,
        Unsupported
    }

    /// <summary>在向调用方交出有效的隐藏 ViewLease 前，持有部分构造资源。</summary>
    public interface IViewProvider
    {
        SyncCreateAvailability GetSyncAvailability(ViewResource resource);

        IViewLease Create(ViewResource resource);

        ValueTask<IViewLease> CreateAsync(ViewResource resource, CancellationToken cancellationToken);
    }
}
