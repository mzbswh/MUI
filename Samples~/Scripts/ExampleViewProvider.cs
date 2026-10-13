using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Examples
{
    /// <summary>一个宿主、两个制作好的挂点：普通课程可滚动，弹窗在全屏区域居中。</summary>
    internal sealed class ExampleViewProvider : IViewProvider, IAsyncDisposable
    {
        private readonly LoadedPrefabViewProvider pages;
        private readonly LoadedPrefabViewProvider overlays;

        public ExampleViewProvider(Transform pageRoot, Transform dialogRoot)
        {
            var loader = new UnityResourcesLoader();
            pages = new LoadedPrefabViewProvider(pageRoot, loader,
                configureView: view => view.ConfigureResources(loader));
            overlays = new LoadedPrefabViewProvider(dialogRoot, loader,
                configureView: view => view.ConfigureResources(loader));
        }

        public Task<IAcquiredView> AcquireAsync(ViewResource resource, CancellationToken cancellationToken = default)
        {
            switch (resource.Key)
            {
                case "MUIExamples/Detail":
                case "MUIExamples/Confirmation":
                case "MUIExamples/Alert":
                    return overlays.AcquireAsync(resource, cancellationToken);
                default:
                    return pages.AcquireAsync(resource, cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await overlays.DisposeAsync();
            }
            finally
            {
                await pages.DisposeAsync();
            }
        }
    }
}
