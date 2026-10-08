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

        /// <summary>直接读取准备执行状态，不创建完成任务。</summary>
        internal bool IsPreparing => preparationExecuting;

        /// <summary>关闭等待准备代码退出，防止提前释放仍被准备流程使用的资源。</summary>
        protected Task PreparationExecutionCompletion
        {
            get
            {

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
