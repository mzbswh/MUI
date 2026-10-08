using System;
using MUI.Resources;

namespace MUI.UGUI
{
    internal sealed partial class ResourceSlot<T> where T : class
    {
        private readonly object diagnosticGate = new object();
        private Exception firstCleanupFailure;
        private long cleanupFailureCount;

        /// <summary>目标可能已引用未提交的资源；清理成功清空目标后恢复为 false，仅在所属 UI 线程读取。</summary>
        public bool HasUncertainAssignment
        {
            get
            {
                RequireThread();
                return assignmentUncertain;
            }
        }

        /// <summary>已记录的首个目标清空、资源归还或加载回滚失败，不保留无界异常列表。</summary>
        public Exception FirstCleanupFailure
        {
            get
            {
                lock (diagnosticGate)
                {
                    return firstCleanupFailure;
                }
            }
        }

        /// <summary>目标清空、资源归还与加载回滚失败次数，达到 long.MaxValue 后保持饱和。</summary>
        public long CleanupFailureCount
        {
            get
            {
                lock (diagnosticGate)
                {
                    return cleanupFailureCount;
                }
            }
        }

        private void RecordReleaseError(Exception error, bool recordInLifetime = true)
        {
            lock (diagnosticGate)
            {
                if (firstCleanupFailure == null)
                {
                    firstCleanupFailure = error;
                }
                if (cleanupFailureCount < long.MaxValue)
                {
                    ++cleanupFailureCount;
                }
            }

            // 普通归还错误先登记到生命周期；目标清空错误由当前清理回调抛出后汇总。
            if (recordInLifetime)
            {
                lifetime.RecordCleanupFailure(error);
            }
            UIErrors.Report(error);
        }
    }
}
