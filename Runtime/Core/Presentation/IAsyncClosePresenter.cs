using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    public interface IAsyncClosePresenter
    {
        ValueTask OnCloseAsync(CancellationToken cancellationToken);
    }
}
