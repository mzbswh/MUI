using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Samples.Navigation
{
    /// <summary>演示已启动 I/O 无法取消的资源系统。</summary>
    public sealed class DelayedItemProvider : IViewProvider
    {
        private readonly IViewProvider inner;

        public DelayedItemProvider(IViewProvider inner)
        {
            this.inner = inner;
        }

        public SyncCreateAvailability GetSyncAvailability(ViewResource resource) => resource.Key == "SlowThingItem"
                    ? SyncCreateAvailability.Unsupported : inner.GetSyncAvailability(resource);

        public IViewLease Create(ViewResource resource) => resource.Key == "SlowThingItem"
                    ? throw new NotSupportedException("Slow sample content needs asynchronous loading.") : inner.Create(resource);

        public async ValueTask<IViewLease> CreateAsync(ViewResource resource, CancellationToken token)
        {
            if (resource.Key != "SlowThingItem")
            {
                return await inner.CreateAsync(resource, token);
            }

            await Task.Delay(150); // 故意延迟完成，演示迟到凭证的所有权处理。
            return await inner.CreateAsync(new ViewResource("ThingItem"), CancellationToken.None);
        }
    }
}
