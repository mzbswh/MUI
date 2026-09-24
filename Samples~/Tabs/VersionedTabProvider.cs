using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;

namespace MUI.Samples.Tabs
{
    /// <summary>为固定同步 Prefab 提供方增加演示代际，模拟内容目录更新。</summary>
    internal sealed class VersionedTabProvider : IVersionedViewProvider
    {
        private readonly IViewProvider provider;

        internal VersionedTabProvider(IViewProvider provider)
        {
            this.provider = provider;
        }

        public object ContentVersion { get; private set; } = new object();

        internal int CreatedCount
        {
            get; private set;
        }

        internal void Invalidate() => ContentVersion = new object();

        public SyncCreateAvailability GetSyncAvailability(ViewResource resource) => provider.GetSyncAvailability(resource);

        public IViewLease Create(ViewResource resource)
        {
            var lease = provider.Create(resource);
            CreatedCount++;
            return lease;
        }

        public ValueTask<IViewLease> CreateAsync(ViewResource resource, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IViewLease>(Create(resource));
        }
    }
}
