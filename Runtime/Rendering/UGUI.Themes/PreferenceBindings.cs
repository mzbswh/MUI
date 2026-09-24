using System;
using System.Threading;
using MUI.Themes;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public static class PreferenceBindings
    {
        public static IDisposable BindFontSize(ThemeService themes,
                    UIUserPreferences preferences,
                    Lifetime lifetime,
                    Text text,
                    ThemeToken<float> token)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.resizeTextForBestFit)
            {
                throw new InvalidOperationException("Disable Text best-fit before binding a scaled font size.");
            }

            return FontSizeBinding.Bind(themes, preferences, lifetime, token, size =>
            {
                if (text == null)
                {
                    throw new InvalidOperationException("The scaled text was destroyed.");
                }

                if (text.resizeTextForBestFit)
                {
                    throw new InvalidOperationException("Text best-fit conflicts with the scaled font binding.");
                }

                if ((double)size > int.MaxValue)
                {
                    throw new InvalidOperationException("Scaled font size exceeds the native integer range.");
                }

                var rounded = Math.Max(1, (int)Math.Round((double)size, MidpointRounding.AwayFromZero));
                if (text.fontSize != rounded)
                {
                    text.fontSize = rounded;
                }
            });
        }

        /// <summary>只控制 ColorTint 时长；完整页面转场由其转场提供方处理。</summary>
        public static IDisposable BindReducedMotion(UIUserPreferences preferences, Lifetime lifetime, Selectable selectable)
        {
            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            if (selectable == null)
            {
                throw new ArgumentNullException(nameof(selectable));
            }

            if (selectable.transition != Selectable.Transition.ColorTint)
            {
                throw new InvalidOperationException("Reduced-motion tint binding requires ColorTint.");
            }

            var authoredDuration = selectable.colors.fadeDuration;
            var binding = new MotionBinding(selectable, authoredDuration);
            try
            {
                binding.Subscription = preferences.Observe(lifetime, value =>
                {
                    if (selectable == null)
                    {
                        throw new InvalidOperationException("The motion-controlled Selectable was destroyed.");
                    }

                    if (selectable.transition != Selectable.Transition.ColorTint)
                    {
                        throw new InvalidOperationException("Selectable transition changed away from ColorTint.");
                    }

                    var colors = selectable.colors;
                    var duration = value.ReducedMotion ? 0 : authoredDuration;
                    if (colors.fadeDuration == duration)
                    {
                        return;
                    }

                    colors.fadeDuration = duration;
                    selectable.colors = colors;
                });
                return lifetime.OwnDisposable(binding);
            }
            catch (Exception failure)
            {
                try
                {
                    binding.Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(failure, cleanup);
                }

                throw;
            }
        }

        private sealed class MotionBinding : IDisposable
        {
            internal IDisposable Subscription;
            private Selectable target;
            private readonly float authoredDuration;
            private readonly int thread = Thread.CurrentThread.ManagedThreadId;

            internal MotionBinding(Selectable target, float authoredDuration)
            {
                this.target = target;
                this.authoredDuration = authoredDuration;
            }

            public void Dispose()
            {
                if (Thread.CurrentThread.ManagedThreadId != thread)
                {
                    throw new InvalidOperationException("Motion binding requires its owning UI thread.");
                }

                if (Subscription != null)
                {
                    Subscription.Dispose();
                    Subscription = null;
                }

                var control = target;
                target = null;
                if (control == null)
                {
                    return;
                }

                // 保留作者配置的基准值，供池化节点下次绑定使用。
                var colors = control.colors;
                if (colors.fadeDuration == authoredDuration)
                {
                    return;
                }

                colors.fadeDuration = authoredDuration;
                control.colors = colors;
            }
        }
    }
}
