using System;
using System.Threading.Tasks;
using MUI.Loading;
using MUI.Notifications;
using MUI.Themes;
using MUI.UGUI;

namespace MUI.Examples
{
    /// <summary>Project presentation state; services own timing, queues and input gates.</summary>
    [UIPage(typeof(Unit), typeof(Unit), ResourceKey = "MUIExamples/Feedback")]
    public partial class FeedbackViewModel : ViewModel, IAsyncDisposable
    {
        public readonly LifetimeScope Lifetime = new LifetimeScope();
        public readonly ThemeService Themes;
        public static readonly ThemeToken<ThemeColor> Surface = new ThemeToken<ThemeColor>("surface");
        private readonly NotificationQueue notifications;
        private readonly LoadingScope loading;
        private readonly InputGate input;
        private bool alternateTheme;

        public FeedbackViewModel()
        {
            notifications = Lifetime.OwnDisposable(new NotificationQueue());
            loading = Lifetime.OwnDisposable(new LoadingScope(0.15));
            input = Lifetime.OwnDisposable(new InputGate());
            Themes = Lifetime.OwnDisposable(new ThemeService(Catalog(false)));
            notifications.Changed += RenderNotification;
            loading.Changed += RenderProgress;
            input.Changed += RefreshCommands;
            Lifetime.OnDispose(() =>
            {
                notifications.Changed -= RenderNotification;
                loading.Changed -= RenderProgress;
                input.Changed -= RefreshCommands;
            });
        }

        private bool CanRun => input.IsOpen;

        private void RefreshCommands() => OnPropertyChanged(nameof(CanRun));

        private void RenderNotification(NotificationSnapshot snapshot)
        {
            ToastVisible = snapshot.IsVisible;
            if (snapshot.IsVisible)
            {
                ToastMessage = snapshot.Current.Message;
            }
        }

        private void RenderProgress(LoadingSnapshot snapshot) => Progress = snapshot.IsVisible
            ? "正在同步偏好设置：" + ((snapshot.Progress ?? 0) * 100).ToString("0") + "%"
            : "已就绪，可以继续操作";

        public void Advance(float delta)
        {
            if (!Lifetime.IsEnded)
            {
                notifications.Advance(delta);
                loading.Advance(delta);
            }
        }

        [Command(CanExecute = nameof(CanRun))]
        private void Toast() => notifications.Post(new Notification("偏好设置已保存。", displayDuration: 3));

        [Command(CanExecute = nameof(CanRun))]
        private async ValueTask RunAsync(CommandContext context)
        {
            using (var operation = loading.Begin(Lifetime, "同步偏好设置", blockInput: input))
            {
                for (var i = 1; i <= 8; ++i)
                {
                    await Task.Delay(180, context.Token);
                    operation.Report(i / 8d);
                }
            }

            context.Apply(Toast);
        }

        [Command]
        private void ClearNotifications()
        {
            notifications.Clear();
            QueueLabel = "通知队列已清空";
        }

        [Command]
        private void Theme()
        {
            alternateTheme = !alternateTheme;
            Themes.SetCatalog(Catalog(alternateTheme));
            ThemeLabel = alternateTheme ? "当前主题 · 海洋" : "当前主题 · 深夜";
        }

        private static ThemeCatalog Catalog(bool ocean) => new ThemeCatalog(ocean ? "ocean" : "midnight", new[] { ThemeValue.Create(Surface, ocean ? new ThemeColor(0.10f, 0.25f, 0.28f) : new ThemeColor(0.11f, 0.13f, 0.18f)) });

        public ValueTask DisposeAsync() => Lifetime.DisposeAsync();
    }
}
