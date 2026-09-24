using System.Threading;

namespace MUI
{
    /// <summary>独立同步命令契约；执行期间不得启动后台工作或阻塞等待异步任务。</summary>
    public interface ISynchronousUICommand : IUICommandState
    {
        CommandOutcome Execute(ICommandTarget source = null, CancellationToken cancellationToken = default);
    }
}
