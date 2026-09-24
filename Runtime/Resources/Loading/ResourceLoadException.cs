using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>加载失败且后端仍在回滚；调用方可等待清理结束，不自行卸载资源。</summary>
    public sealed class ResourceLoadException : Exception
    {
        /// <summary>由资源后端报告失败原因及实际回滚任务；不能用于纯同步加载协议。</summary>
        public ResourceLoadException(Exception cause, Task cleanupCompletion)
            : base("Resource load failed; observe cleanup completion for physical release.", cause ?? throw new ArgumentNullException(nameof(cause)))
        {
            CleanupCompletion = cleanupCompletion ?? throw new ArgumentNullException(nameof(cleanupCompletion));
        }

        public Task CleanupCompletion
        {
            get;
        }
    }
}
