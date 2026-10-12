using System;
using System.Threading;
using System.Threading.Tasks;
namespace MUI.Samples.Tabs
{
    public sealed class QuestTabPresenter : Presenter<QuestTabViewModel, TabPageArgs, Unit>, IAsyncOpenPreparation<TabPageArgs>
    {
        public async ValueTask OnPrepareOpenAsync(TabPageArgs args, CancellationToken token)
        {
            await Task.Delay(args.DelayMilliseconds, args.IgnorePreparationCancellation ? CancellationToken.None : token);
            token.ThrowIfCancellationRequested();
            if (args.Fail)
                throw new InvalidOperationException("Quest content temporarily unavailable.");
            Context.Apply(() => ViewModel.Title = args.Title + " · quest journal");
        }
    }
}
