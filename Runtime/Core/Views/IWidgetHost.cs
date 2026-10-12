using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>可选子项所有权能力，在绑定和打开前进入。
    /// 仅可见性变化不能结束子项激活。</summary>
    public interface IWidgetHost
    {
        void BeginWidgetActivation(LifetimeScope activation);

        void CommitWidgetActivation();

        bool TryCompleteWidgetPreparation();

        ValueTask CompleteWidgetPreparationAsync(CancellationToken cancellationToken);
    }
}
