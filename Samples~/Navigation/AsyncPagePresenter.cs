using System.Threading;
using System.Threading.Tasks;
using MUI.Pages;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed class AsyncPagePresenter : PagePresenter, IAsyncOpenPresenter<PageArgs>, IAsyncClosePresenter
    {
        private readonly PageManager pages;
        private readonly PageDefinition<PageViewModel, PageArgs, int> otherRoute;

        public AsyncPagePresenter(PageManager pages, PageDefinition<PageViewModel, PageArgs, int> otherRoute)
        {
            this.pages = pages;
            this.otherRoute = otherRoute;
        }

        public async ValueTask OnOpenAsync(PageArgs args, CancellationToken cancellationToken)
        {
            Debug.Log("MUI Navigation OnOpenAsync begin");
            await Task.Delay(100, cancellationToken);
            var nested = await pages.OpenAsync(otherRoute, new PageArgs("Should be rejected", 0));
            Debug.Log("MUI Navigation nested open: " + nested.Rejection);
            Context.Apply(() => ViewModel.Title = args.Title + " (ready)");
            Debug.Log("MUI Navigation OnOpenAsync ready");
        }

        public async ValueTask OnCloseAsync(CancellationToken cancellationToken)
        {
            Debug.Log("MUI Navigation OnCloseAsync begin");
            await Task.Delay(100, cancellationToken);
            Debug.Log("MUI Navigation OnCloseAsync end");
        }
    }
}
