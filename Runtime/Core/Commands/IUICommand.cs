using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    public interface IUICommand : IUICommandState
    {
        ValueTask<CommandOutcome> ExecuteAsync(ICommandTarget source = null, CancellationToken cancellationToken = default);
    }
}
