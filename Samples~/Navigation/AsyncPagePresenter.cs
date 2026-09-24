using System.Threading;
using System.Threading.Tasks;
using MUI.Navigation;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed class AsyncPagePresenter : PagePresenter, IAsyncOpenPresenter<PageArgs>, IAsyncClosePresenter
    {
        private readonly Navigator navigator;
        private readonly Route<PageViewModel, PageArgs, int> otherRoute;

        public AsyncPagePresenter(Navigator navigator, Route<PageViewModel, PageArgs, int> otherRoute)
        {
            this.navigator = navigator;
            this.otherRoute = otherRoute;
        }

        public async ValueTask OnOpenAsync(PageArgs args, CancellationToken cancellationToken)
        {
            Debug.Log("MUI Navigation OnOpenAsync begin");
            await Task.Delay(100, cancellationToken);
            var nested = await navigator.OpenAsync(otherRoute, new PageArgs("Should be rejected", 0));
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
