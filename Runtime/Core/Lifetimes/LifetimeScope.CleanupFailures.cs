using System;
using System.Collections.Generic;

namespace MUI
{
    public sealed partial class LifetimeScope
    {
        private Exception firstOperationCleanupFailure;
        private long operationCleanupFailureCount;

        /// <summary>
        /// 记录所属操作已发生的回滚或资源归还失败，使最终释放也报告失败。
        /// 仅用于清理错误，业务操作失败仍由调用者处理；保留首错和饱和计数，避免异常无界积累。
        /// 须在所属操作退出前登记，释放完成后不能追加。
        /// </summary>
        public void RecordCleanupFailure(Exception failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            lock (gate)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(LifetimeScope));
                }
                if (firstOperationCleanupFailure == null)
                {
                    firstOperationCleanupFailure = failure;
                }
                if (operationCleanupFailureCount < long.MaxValue)
                {
                    ++operationCleanupFailureCount;
                }
            }
        }

        private void AppendOperationCleanupFailures(List<Exception> errors)
        {
            if (firstOperationCleanupFailure == null)
            {
                return;
            }
            errors.Add(firstOperationCleanupFailure);
            if (operationCleanupFailureCount > 1)
            {
                errors.Add(new InvalidOperationException($"另有 {operationCleanupFailureCount - 1} 次操作清理失败，未保留完整异常。"));
            }
        }
    }
}
