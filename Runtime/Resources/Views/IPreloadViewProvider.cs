using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    public interface IPreloadViewProvider : IViewProvider
    {
        ValueTask<IPreloadLease> PreloadAsync(ViewResource resource, CancellationToken cancellationToken = default);
    }
}
