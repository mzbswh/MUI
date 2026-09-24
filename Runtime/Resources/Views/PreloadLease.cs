using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    public sealed class PreloadLease : IPreloadLease
    {
        private readonly ResourceLease<ViewResource> lease;

        public PreloadLease(ViewResource resource, Func<ValueTask> release)
        {
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            lease = new ResourceLease<ViewResource>(resource, _ => release());
        }

        public ViewResource Resource
        {
            get;
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
