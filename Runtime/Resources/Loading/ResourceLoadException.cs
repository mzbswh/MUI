using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>加载失败且后端回滚尚未确认成功；调用方等待清理结果，不自行卸载资源。</summary>
    public sealed class ResourceLoadException : Exception
    {
        /// <summary>由资源后端报告失败原因及实际回滚任务，包括立即失败但仍需等待原生销毁的加载。</summary>
        public ResourceLoadException(Exception cause, Task cleanupCompletion)
            : base("Resource load failed; observe cleanup completion for physical release.", cause ?? throw new ArgumentNullException(nameof(cause)))
        {
            CleanupCompletion = cleanupCompletion ?? throw new ArgumentNullException(nameof(cleanupCompletion));
        }

        /// <summary>后端实际回滚结果；立即回滚失败使用 Task.FromException，不创建另一套同步异常协议。</summary>
        public Task CleanupCompletion
        {
            get;
        }
    }
}
