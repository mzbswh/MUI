using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    public sealed class AcquiredPreload : IAcquiredPreload, ICleanupResponsibilitySource
    {
        private readonly AcquiredResource<ViewResource> ownedResource;

        public AcquiredPreload(ViewResource resource, Func<ValueTask> release, int? releaseThreadId = null,
            bool supportsIdempotentRetry = false, string owner = null)
        {
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            ownedResource = new AcquiredResource<ViewResource>(resource, _ => release(), releaseThreadId,
                supportsIdempotentRetry, owner ?? "AcquiredPreload");
        }

        public ViewResource Resource
        {
            get;
        }

        public CleanupResponsibility CleanupResponsibility => ownedResource.CleanupResponsibility;

        public ValueTask DisposeAsync() => ownedResource.DisposeAsync();
    }
}
