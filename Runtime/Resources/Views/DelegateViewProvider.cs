using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Resources
{
    /// <summary>通过项目委托取得视图凭证；同步工厂和异步加载共享同一获取契约。</summary>
    public sealed class DelegateViewProvider : IViewProvider
    {
        private readonly Func<ViewResource, CancellationToken, Task<IAcquiredView>> acquire;

        /// <summary>接入异步后端；委托负责交付前回滚，返回的凭证由调用方归还。</summary>
        public DelegateViewProvider(Func<ViewResource, CancellationToken, Task<IAcquiredView>> acquire)
        {
            this.acquire = acquire ?? throw new ArgumentNullException(nameof(acquire));
        }

        /// <summary>接入本地工厂，直接返回完成任务，不调度线程或添加等待帧。</summary>
        public DelegateViewProvider(Func<ViewResource, IAcquiredView> acquire)
        {
            if (acquire == null)
            {
                throw new ArgumentNullException(nameof(acquire));
            }

            this.acquire = (resource, _) => Task.FromResult(acquire(resource));
        }

        public Task<IAcquiredView> AcquireAsync(ViewResource resource, CancellationToken cancellationToken = default)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            cancellationToken.ThrowIfCancellationRequested();
            // 不在回调返回后重新检查取消；已经交付的迟到凭证仍须交给调用方清理。
            return acquire(resource, cancellationToken) ??
                throw new InvalidOperationException("The view acquisition delegate returned no task.");
        }
    }
}
