using System;
using System.Threading;

namespace MUI.UGUI
{
    public sealed partial class UIAutomation
    {
        private bool evaluatingCondition;

        /// <summary>
        /// 立即检查一次只读条件，并返回由调用方每帧 Poll 的等待句柄。
        /// 不创建任务、计时器或后台循环；超时和取消仅在 Poll 时观测。
        /// 条件可查询外部导航或模型状态，因此 View 关闭不会自动取消等待。
        /// </summary>
        public UIAutomationConditionWait WaitForCondition(Func<bool> condition, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            RequireThread();
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }
            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }
            if (dispatching || evaluatingCondition)
            {
                throw new InvalidOperationException("不能在自动化派发或条件检查中开始新的等待。");
            }

            var wait = new UIAutomationConditionWait(() => EvaluateCondition(condition), timeout, cancellationToken);
            wait.Poll();
            return wait;
        }

        private bool EvaluateCondition(Func<bool> condition)
        {
            RequireThread();
            if (dispatching || evaluatingCondition)
            {
                throw new InvalidOperationException("自动化条件检查不能重入派发或其他条件检查。");
            }
            evaluatingCondition = true;
            try
            {
                return condition();
            }
            finally
            {
                evaluatingCondition = false;
            }
        }
    }
}
