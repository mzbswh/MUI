using System;
using System.Diagnostics;
using System.Threading;

namespace MUI.UGUI
{
    /// <summary>条件等待的结果；终态不会因后续轮询而改变。</summary>
    public enum UIAutomationConditionStatus
    {
        Pending,
        Satisfied,
        TimedOut,
        Cancelled,
        Failed
    }

    /// <summary>
    /// 调用方拥有的无任务等待句柄，只能在创建线程轮询或取消。
    /// 不自动注册更新；不用时应 Dispose。进入终态后释放条件与取消令牌引用。
    /// </summary>
    public sealed class UIAutomationConditionWait : IDisposable
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly TimeSpan timeout;
        private Func<bool> condition;
        private CancellationToken cancellationToken;
        private bool checkedOnce;
        private bool polling;

        internal UIAutomationConditionWait(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
        {
            this.condition = condition;
            this.timeout = timeout;
            this.cancellationToken = cancellationToken;
        }

        public UIAutomationConditionStatus Status
        {
            get; private set;
        }

        /// <summary>最近一次轮询或取消时的单调时钟耗时，终态后保持不变。</summary>
        public TimeSpan Elapsed
        {
            get; private set;
        }

        /// <summary>条件抛出的异常；仅 Failed 终态有值，由调用者决定是否记录或重抛。</summary>
        public Exception Error
        {
            get; private set;
        }

        /// <summary>
        /// 至多执行一次条件。首次允许零超时即时检查，后续到期不再调用条件。
        /// 取消优先于结果；条件执行超过期限时返回超时，不能中断项目同步代码。
        /// </summary>
        public UIAutomationConditionStatus Poll()
        {
            RequireThread();
            if (Status != UIAutomationConditionStatus.Pending)
            {
                return Status;
            }
            if (polling)
            {
                throw new InvalidOperationException("条件等待不能重入轮询自身。");
            }
            UpdateElapsed();
            if (cancellationToken.IsCancellationRequested)
            {
                return Finish(UIAutomationConditionStatus.Cancelled);
            }
            if (checkedOnce && Elapsed >= timeout)
            {
                return Finish(UIAutomationConditionStatus.TimedOut);
            }

            checkedOnce = true;
            polling = true;
            try
            {
                var satisfied = condition();
                // 条件回调可能显式取消此句柄，不能覆盖已产生的终态。
                if (Status != UIAutomationConditionStatus.Pending)
                {
                    return Status;
                }
                UpdateElapsed();
                if (cancellationToken.IsCancellationRequested)
                {
                    return Finish(UIAutomationConditionStatus.Cancelled);
                }
                if (timeout > TimeSpan.Zero && Elapsed >= timeout)
                {
                    return Finish(UIAutomationConditionStatus.TimedOut);
                }
                return satisfied ? Finish(UIAutomationConditionStatus.Satisfied)
                    : timeout == TimeSpan.Zero ? Finish(UIAutomationConditionStatus.TimedOut) : Status;
            }
            catch (Exception error)
            {
                if (Status == UIAutomationConditionStatus.Pending)
                {
                    UpdateElapsed();
                    Error = error;
                    Finish(UIAutomationConditionStatus.Failed);
                }
                return Status;
            }
            finally
            {
                polling = false;
            }
        }

        /// <summary>立即取消尚未完成的等待，不取消导航或业务操作。</summary>
        public void Dispose()
        {
            RequireThread();
            if (Status == UIAutomationConditionStatus.Pending)
            {
                UpdateElapsed();
                Finish(UIAutomationConditionStatus.Cancelled);
            }
        }

        private UIAutomationConditionStatus Finish(UIAutomationConditionStatus status)
        {
            Status = status;
            condition = null;
            cancellationToken = default;
            return status;
        }

        private void UpdateElapsed() => Elapsed = TimeSpan.FromSeconds(
            (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency);

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("条件等待只能在创建它的 Unity 主线程操作。");
            }
        }
    }
}
