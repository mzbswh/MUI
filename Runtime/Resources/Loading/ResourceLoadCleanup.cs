using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>失败加载的回滚仍属于原操作，不能因为后端没有交出凭证就提前结束所有者清理。</summary>
    internal static class ResourceLoadCleanup
    {
        internal static async ValueTask AwaitAsync(Exception failure, LifetimeScope lifetime)
        {
            if (failure is ResourceLoadException pending)
            {
                var rollback = new CleanupResponsibility(() => new ValueTask(pending.CleanupCompletion),
                    "LifetimeScope.PreloadRollback");
                await CleanupRegistry.ReleaseAsync(rollback, rollback.Owner, lifetime);
            }
        }

        internal static Exception Cause(Exception failure)
        {
            while (failure is ResourceLoadException && failure.InnerException != null)
            {
                failure = failure.InnerException;
            }
            return failure;
        }
    }
}
