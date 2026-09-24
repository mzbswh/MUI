using System;

namespace MUI
{
    /// <summary>同步参数准备契约；准备阶段只构建隔离候选，不得修改当前模型。</summary>
    public interface ISynchronousArgsUpdatePresenter<in TArgs>
    {
        ISynchronousPreparedArgsUpdate PrepareArgsUpdate(TArgs args);
    }

    /// <summary>
    /// 同步候选，框架取得唯一释放权。Commit 应用状态，部分提交失败也必须能 Rollback；
    /// Dispose 无条件释放尚未移交的资源，成功提交的资源应交给实例或激活 Lifetime。
    /// </summary>
    public interface ISynchronousPreparedArgsUpdate : IDisposable
    {
        void Commit();

        void Rollback();
    }
}
