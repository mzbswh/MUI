using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    public interface IUICommand : IUICommandState
    {
        /// <summary>等待本次命令并取得结果；仅需触发时可使用 Execute 扩展方法。</summary>
        ValueTask<CommandOutcome> ExecuteAsync(ICommandTarget source = null, CancellationToken cancellationToken = default);
    }
}
