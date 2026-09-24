using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    public sealed partial class ThingItemPresenter : Presenter<ThingItemViewModel, string, Unit>, IAsyncOpenPresenter<string>, IArgsUpdatePresenter<string>
    {
        public int OpenCount
        {
            get; private set;
        }

        protected override void OnOpen(string args)
        {
            ++OpenCount;
            ViewModel.Label = args + " — preparing";
        }

        public async ValueTask OnOpenAsync(string args, CancellationToken cancellationToken)
        {
            await Task.Delay(80, cancellationToken);
            Context.Apply(() => ViewModel.Label = args + " × 20");
        }
    }
}
