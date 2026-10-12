using System;
using System.Threading.Tasks;

namespace MUI.Widgets
{
    internal sealed partial class WidgetInstance<TViewModel, TArgs> where TViewModel : ViewModel
    {
        /// <summary>异步过渡占位，回调开始前撤销旧状态资格。</summary>
        private TaskCompletionSource<bool> BeginLifecycleTransition(WidgetState state)
        {
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lifecycleTransitionFinished = finished.Task;
            State = state;
            return finished;
        }

        /// <summary>旧激活完全清理后建立新的激活，不重新创建实例、模型或 Presenter。</summary>
        private void BeginFreshActivation()
        {
            activation = new LifetimeScope();
            activationCleanupComplete = false;
        }

        private void RecordTransitionFailure(Exception error)
        {
            if (!(error is OperationCanceledException))
            {
                earlyErrors.Add(error);
            }
        }
    }
}
