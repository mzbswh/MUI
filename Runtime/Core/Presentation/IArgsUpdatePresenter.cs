using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>显式参数更新能力；准备阶段只构建隔离候选，不得修改当前模型或持有等待自身关闭的任务。</summary>
    public interface IArgsUpdatePresenter<in TArgs>
    {
        ValueTask<IPreparedArgsUpdate> PrepareArgsUpdateAsync(TArgs args, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 一次参数更新的候选，框架取得其唯一释放权。
    /// Commit 在主线程同步应用新状态；异常时框架撤销输入并故障关闭，不回滚业务副作用。
    /// DisposeAsync 无条件收尾，释放未移交的候选资源；成功提交所需资源须在 Commit 中交给适当生命周期。
    /// 所有回调在 UI 线程执行，不得等待导航、关闭或再次更新自身。
    /// </summary>
    public interface IPreparedArgsUpdate : IAsyncDisposable
    {
        void Commit();
    }
}
