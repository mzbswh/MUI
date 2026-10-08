using System;
using System.Threading.Tasks;

namespace MUI.Resources
{
    public sealed class AcquiredPreload : IAcquiredPreload
    {
        private readonly AcquiredResource<ViewResource> ownedResource;

        public AcquiredPreload(ViewResource resource, Func<ValueTask> release, int? releaseThreadId = null)
        {
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            ownedResource = new AcquiredResource<ViewResource>(resource, _ => release(), releaseThreadId);
        }

        public ViewResource Resource
        {
            get;
        }

        public ValueTask DisposeAsync() => ownedResource.DisposeAsync();
    }
}
