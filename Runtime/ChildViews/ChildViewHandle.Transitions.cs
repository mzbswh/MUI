using System;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    public sealed partial class ChildViewHandle<TViewModel, TArgs> where TViewModel : ViewModel
    {
        /// <summary>异步过渡占位，回调开始前撤销旧状态资格。</summary>
        private TaskCompletionSource<bool> BeginLifecycleTransition(ChildViewState state)
        {
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lifecycleTransitionFinished = finished.Task;
            State = state;
            return finished;
        }

        /// <summary>旧激活完全清理后建立新的激活，不重新创建实例、模型或 Presenter。</summary>
        private void BeginFreshActivation()
        {
            activation = new Lifetime(Owner.Mode);
            activationCleanupComplete = false;
            tickRemainder = 0;
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
