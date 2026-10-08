using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.ResourceIntegration
{
    public sealed partial class LoadedPrefabViewProvider
    {
        private readonly LifetimeScope rollbacks = new LifetimeScope();
        private Exception firstCleanupFailure;
        private int cleanupFailureCount;

        /// <summary>提供方持有的失败创建回滚数；外部正常凭证的释放不计入此数。</summary>
        public int PendingRollbackCount => rollbacks.PendingOperationCount;

        /// <summary>只保留首个错误和饱和计数，避免长期运行时错误目录无限增长。</summary>
        public int CleanupFailureCount
        {
            get
            {
                RequireThread();
                return cleanupFailureCount;
            }
        }

        /// <summary>首个已知清理错误，提供方关闭后仍可查询迟到释放产生的错误。</summary>
        public Exception FirstCleanupFailure
        {
            get
            {
                RequireThread();
                return firstCleanupFailure;
            }
        }

        private void RecordCleanupFailure(Exception error)
        {
            RequireThread();
            if (firstCleanupFailure == null)
            {
                firstCleanupFailure = error;
            }

            if (cleanupFailureCount < int.MaxValue)
            {
                ++cleanupFailureCount;
            }
        }

        private Task TrackFailedCreation(Entry entry, Exception failure)
        {
            // Create 已登记到 loading；退出先排空创建，再结束回滚登记，避免回调内退出的竞态。
            var operation = rollbacks.RunAsync(async _ =>
            {
                await ReleaseFailedCreationAsync(entry, failure);
                return true;
            });
            var completion = operation.AsTask();
            _ = ObserveRollbackAsync(completion);
            return completion;
        }

        private static async Task ObserveRollbackAsync(Task<bool> operation)
        {
            try
            {
                await operation;
            }
            catch (Exception error)
            {
                UIErrors.Report(error);
            }
        }

        private async Task DisposeCoreAsync()
        {
            var errors = new List<Exception>();
            try
            {
                await loading.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                await rollbacks.DisposeAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            // 排空创建与回滚后再断开配置引用，避免影响仍在执行的加载。
            // 外部凭证通过自身持有的资源释放，不依赖这些借用配置。
            loader = null;
            resolveKey = null;
            configureView = null;

            try
            {
                if (staging != null)
                {
                    UnityEngine.Object.Destroy(staging);
                }
            }
            catch (Exception error)
            {
                RecordCleanupFailure(error);
            }

            if (firstCleanupFailure != null && !errors.Contains(firstCleanupFailure))
            {
                errors.Add(firstCleanupFailure);
            }

            if (errors.Count == 0)
            {
                disposal.TrySetResult(true);
            }
            else
            {
                disposal.TrySetException(new AggregateException("Loaded prefab provider cleanup failed.", errors));
                _ = disposal.Task.Exception;
            }
            // 正常视图和预加载凭证仍归调用者；迟到释放错误可通过诊断属性查询。
        }
    }
}
