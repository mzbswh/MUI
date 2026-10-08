using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>失败加载的回滚仍属于原操作，不能因为后端没有交出凭证就提前结束所有者清理。</summary>
    internal static class ResourceLoadCleanup
    {
        internal static async ValueTask AwaitAsync(Exception failure)
        {
            if (failure is ResourceLoadException pending)
            {
                await pending.CleanupCompletion;
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
