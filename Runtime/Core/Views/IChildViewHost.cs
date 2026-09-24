using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>可选子项所有权能力，在绑定和打开前进入。
    /// 仅可见性变化不能结束子项激活。</summary>
    public interface IChildViewHost
    {
        void BeginChildActivation(Lifetime activation);

        void CommitChildActivation();

        bool TryCompleteChildPreparation();

        ValueTask CompleteChildPreparationAsync(CancellationToken cancellationToken);
    }
}
