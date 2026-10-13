using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.UGUI;

namespace MUI.Examples
{
    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/Navigation")]
    public partial class NavigationViewModel : ViewModel
    {
        private readonly Func<CancellationToken, Task<string>> openDetails;

        public NavigationViewModel(Func<CancellationToken, Task<string>> openDetails) => this.openDetails = openDetails;

        [Command]
        private async ValueTask OpenAsync(CommandContext context)
        {
            var outcome = await openDetails(context.Token);
            context.Apply(() => Result = outcome);
        }
    }

    [UIPage(typeof(Unit), typeof(int), ResourceKey = "MUIExamples/Detail")]
    public partial class DetailViewModel : ViewModel
    {
        [Command]
        private void Complete(CommandContext context) => context.RequestClose(42);

        [Command]
        private void Back(CommandContext context) => context.RequestClose();
    }

    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/Dialogs")]
    public partial class DialogsViewModel : ViewModel
    {
        private readonly Func<bool, CancellationToken, Task<string>> showDialog;

        public DialogsViewModel(Func<bool, CancellationToken, Task<string>> showDialog) => this.showDialog = showDialog;

        [Command]
        private async ValueTask ConfirmAsync(CommandContext context)
        {
            var result = await showDialog(false, context.Token);
            context.Apply(() => Result = result);
        }

        [Command]
        private async ValueTask AlertAsync(CommandContext context)
        {
            var result = await showDialog(true, context.Token);
            context.Apply(() => Result = result);
        }
    }
}
