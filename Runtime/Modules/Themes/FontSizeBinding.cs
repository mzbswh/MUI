using System;
using System.Runtime.ExceptionServices;

namespace MUI.Themes
{
    /// <summary>将主题基准字号与独立用户缩放相乘；始终从基准计算，避免累计缩放。</summary>
    public static class FontSizeBinding
    {
        /// <summary>
        /// 订阅主题字号和用户偏好并立即刷新，两个订阅都归 owner 所有。
        /// 返回组合凭证可提前退订；结果必须为正的有限字号，原生取整由控件适配完成。
        /// </summary>
        public static IDisposable Bind(ThemeService themes, UIUserPreferences preferences, Lifetime owner,
            ThemeToken<float> token, Action<float> apply)
        {
            if (themes == null)
            {
                throw new ArgumentNullException(nameof(themes));
            }

            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            var baseSize = themes.Get(token);
            void Refresh()
            {
                var scaled = baseSize * preferences.FontScale;
                if (float.IsNaN(scaled) || float.IsInfinity(scaled) || scaled <= 0)
                {
                    throw new InvalidOperationException("The scaled font size must be positive and finite.");
                }

                apply(scaled);
            }

            var themeSubscription = themes.Observe(owner, token, value =>
            {
                baseSize = value;
                Refresh();
            });
            try
            {
                var preferenceSubscription = preferences.Observe(owner, value => Refresh());
                return new CombinedSubscription(themeSubscription, preferenceSubscription);
            }
            catch (Exception failure)
            {
                try
                {
                    themeSubscription.Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(failure, cleanup);
                }

                throw;
            }
        }

        private sealed class CombinedSubscription : IDisposable
        {
            private IDisposable theme;
            private IDisposable preferences;

            internal CombinedSubscription(IDisposable theme, IDisposable preferences)
            {
                this.theme = theme;
                this.preferences = preferences;
            }

            public void Dispose()
            {
                // 两项分别尝试退订；失败项保留引用，以便在正确线程或故障恢复后重试。
                Exception failure = null;
                if (preferences != null)
                {
                    try
                    {
                        preferences.Dispose();
                        preferences = null;
                    }
                    catch (Exception error)
                    {
                        failure = error;
                    }
                }

                if (theme != null)
                {
                    try
                    {
                        theme.Dispose();
                        theme = null;
                    }
                    catch (Exception error)
                    {
                        failure = failure == null ? error : new AggregateException(failure, error);
                    }
                }

                if (failure != null)
                {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }
            }
        }
    }
}
