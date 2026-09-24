using System;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    /// <summary>准备失败及其独立清理结果；同步错误直接保存，异步回滚可能仍在进行。</summary>
    public sealed class ChildViewPreparationException : Exception
    {
        private readonly Task asynchronousCleanup;
        private readonly Lazy<Task> synchronousCleanup;

        internal ChildViewPreparationException(Exception cause, Task cleanup) : base("ChildView preparation failed. Observe CleanupCompletion for physical cleanup.", cause)
        {
            asynchronousCleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        }

        private ChildViewPreparationException(Exception failure, Exception cleanup)
                    : base("ChildView preparation and synchronous cleanup failed.",
                        new AggregateException("Child preparation and synchronous cleanup failed.", failure, cleanup))
        {
            SynchronousCleanupError = cleanup;
            // 异常可直接跨同步调用链传播；仅兼容调用者读取任务时才创建失败信号。
            synchronousCleanup = new Lazy<Task>(() =>
            {
                var completion = Task.FromException(cleanup);
                _ = completion.Exception;
                return completion;
            });
        }

        /// <summary>已在调用内发生的同步清理错误；异步清理结果仍通过 CleanupCompletion 获取。</summary>
        public Exception SynchronousCleanupError
        {
            get;
        }

        /// <summary>清理兼容信号；同步失败按需生成，读取本身不会启动清理工作。</summary>
        public Task CleanupCompletion => asynchronousCleanup ?? synchronousCleanup.Value;

        internal static ChildViewPreparationException SynchronousCleanupFailed(Exception failure, Exception cleanup) =>
                    new ChildViewPreparationException(failure, cleanup);
    }
}
