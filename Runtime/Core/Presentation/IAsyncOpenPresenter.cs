using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>显式可选能力；同步 Open 拒绝实现此接口的 Presenter。</summary>
    public interface IAsyncOpenPresenter<in TArgs>
    {
        ValueTask OnOpenAsync(TArgs args, CancellationToken cancellationToken);
    }
}
