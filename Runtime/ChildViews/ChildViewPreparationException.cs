using System;
using System.Threading.Tasks;

namespace MUI.ChildViews
{
    /// <summary>子视图准备失败；物理清理可能在异常抛出后继续。</summary>
    public sealed class ChildViewPreparationException : Exception
    {
        private readonly Task cleanup;

        internal ChildViewPreparationException(Exception cause, Task cleanup) : base("ChildView preparation failed. Observe CleanupCompletion for physical cleanup.", cause)
        {
            this.cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        }

        /// <summary>等待本次候选的物理清理；读取不会启动额外工作。</summary>
        public Task CleanupCompletion => cleanup;
    }
}
