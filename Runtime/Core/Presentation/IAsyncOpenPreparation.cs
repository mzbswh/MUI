using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>异步打开准备能力；框架建立 Context 后等待此方法完成，再提交页面打开。</summary>
    public interface IAsyncOpenPreparation<in TArgs>
    {
        ValueTask OnPrepareOpenAsync(TArgs args, CancellationToken cancellationToken);
    }
}
