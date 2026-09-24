using System;

namespace MUI.Resources
{
    /// <summary>
    /// 同步加载未交出凭证，且部分资源回滚失败，无法确认资源已归还。
    /// 不包含后台清理任务；调用方必须报告未完成的回滚，不能视为普通加载失败。
    /// </summary>
    public sealed class SynchronousResourceLoadException : Exception
    {
        public SynchronousResourceLoadException(Exception loadError, Exception cleanupError)
                    : base("Synchronous resource load failed and rollback did not release all resources.",
                        new AggregateException(loadError ?? throw new ArgumentNullException(nameof(loadError)),
                            cleanupError ?? throw new ArgumentNullException(nameof(cleanupError))))
        {
            LoadError = loadError;
            CleanupError = cleanupError;
        }

        public Exception LoadError
        {
            get;
        }

        public Exception CleanupError
        {
            get;
        }
    }
}
