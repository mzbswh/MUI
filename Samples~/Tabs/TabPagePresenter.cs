using System;
using System.Threading;
using System.Threading.Tasks;

namespace MUI.Samples.Tabs
{
    public readonly struct TabPageArgs
    {
        public TabPageArgs(string title, bool fail = false, int delayMilliseconds = 150, bool ignorePreparationCancellation = false)
        {
            if (delayMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));
            }

            Title = title;
            Fail = fail;
            DelayMilliseconds = delayMilliseconds;
            IgnorePreparationCancellation = ignorePreparationCancellation;
        }

        public string Title
        {
            get;
        }

        public bool Fail
        {
            get;
        }

        public int DelayMilliseconds
        {
            get;
        }

        public bool IgnorePreparationCancellation
        {
            get;
        }
    }

    public sealed class TabPagePresenter : Presenter<TabPageViewModel, TabPageArgs, Unit>, IAsyncOpenPresenter<TabPageArgs>
    {
        public async ValueTask OnOpenAsync(TabPageArgs args, CancellationToken token)
        {
            // 可选地模拟不能即时取消的外部 I/O；完成后仍检查激活资格，绝不回写已关闭页面。
            await Task.Delay(args.DelayMilliseconds, args.IgnorePreparationCancellation ? CancellationToken.None : token);
            token.ThrowIfCancellationRequested();
            if (args.Fail)
            {
                throw new InvalidOperationException("Sample resource temporarily unavailable.");
            }

            Context.Apply(() => ViewModel.Title = args.Title + " content");
        }
    }
}
