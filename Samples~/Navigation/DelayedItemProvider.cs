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

        public async Task<IAcquiredView> AcquireAsync(ViewResource resource, CancellationToken token = default)
        {
            if (resource.Key != "SlowThingItem")
            {
                return await inner.AcquireAsync(resource, token);
            }

            await Task.Delay(150); // 故意延迟完成，演示迟到凭证的所有权处理。
            return await inner.AcquireAsync(new ViewResource("ThingItem"), CancellationToken.None);
        }
    }
}
