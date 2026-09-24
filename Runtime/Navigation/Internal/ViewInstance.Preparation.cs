using System;
using System.Threading.Tasks;

namespace MUI.Navigation
{
    internal abstract partial class ViewInstance
    {
        // 只携带诊断标识，不持有请求参数或追踪缓冲；共享实例复用不改写原始准备归属。
        internal long PreparationOperationId;
        internal object PreparationTraceSession;
        private bool preparationExecuting;
        private TaskCompletionSource<bool> preparationExecutionCompletion;

        /// <summary>直接读取准备执行状态；同步能力检查不读取或创建完成任务。</summary>
        internal bool IsPreparing => preparationExecuting;

        /// <summary>
        /// 仅异步关闭读取。同步 Open 的回调中也可能发起异步关闭，此时按需建立排空信号；
        /// 正常纯同步路径只维护执行标志，不分配任务。
        /// </summary>
        protected Task PreparationExecutionCompletion
        {
            get
            {
                if (Mode == LifetimeMode.Synchronous)
                {
                    throw new InvalidOperationException("纯同步准备不提供内部异步排空信号。");
                }
                if (!preparationExecuting)
                {
                    return Task.CompletedTask;
                }
                if (preparationExecutionCompletion == null)
                {
                    preparationExecutionCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                return preparationExecutionCompletion.Task;
            }
        }

        internal void BeginPreparationExecution(bool asynchronous)
        {
            if (preparationExecuting || PreparationComplete || HasCloseStarted || State != ViewState.Opening)
            {
                throw new InvalidOperationException("界面准备不能重入，也不能在提交或退出后重新开始。");
            }
            if (asynchronous && Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("纯同步界面不能启动异步准备。");
            }
            if (asynchronous)
            {
                preparationExecutionCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            preparationExecuting = true;
        }

        internal void EndPreparationExecution()
        {
            preparationExecuting = false;
            // 准备异常由打开事务处理；此信号只表示业务代码不再使用准备中的资源。
            preparationExecutionCompletion?.TrySetResult(true);
        }
    }
}
