using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Navigation
{
    public sealed partial class ThingItemPresenter : Presenter<ThingItemViewModel, string, Unit>, IAsyncOpenPreparation<string>, IArgsUpdatePresenter<string>
    {
        public int OpenCount
        {
            get; private set;
        }

        protected override void OnPrepareOpen(string args)
        {
            ++OpenCount;
            ViewModel.Label = args + " — preparing";
        }

        public async ValueTask OnPrepareOpenAsync(string args, CancellationToken cancellationToken)
        {
            await Task.Delay(80, cancellationToken);
            Context.Apply(() => ViewModel.Label = args + " × 20");
        }
    }
}
