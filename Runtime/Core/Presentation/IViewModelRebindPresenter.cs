using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>
    /// 可选的换绑准备入口。准备只能持有隔离候选，不能修改当前模型、控件或订阅。
    /// </summary>
    public interface IViewModelRebindPresenter<in TViewModel> where TViewModel : ViewModel
    {
        ValueTask<IPreparedViewModelRebind> PrepareViewModelRebindAsync(
            TViewModel next, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 框架取得候选的释放权。Commit 在 UI 线程同步移交已准备的资源；
    /// 提交所需资源应在其中转给适当的生命周期，DisposeAsync 始终清理未移交部分。
    /// </summary>
    public interface IPreparedViewModelRebind : IAsyncDisposable
    {
        void Commit();
    }
}
