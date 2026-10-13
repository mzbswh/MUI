using System;
using MUI.Themes;
using TMPro;

namespace MUI.TMP
{
    public static class TMPPreferenceBindings
    {
        public static IDisposable BindFontSize(ThemeService themes,
            UIUserPreferences preferences,
            LifetimeScope lifetime,
            TextMeshProUGUI text,
            ThemeToken<float> token)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.enableAutoSizing)
            {
                throw new InvalidOperationException("Disable TMP auto-sizing before binding a scaled font size.");
            }

            return FontSizeBinding.Bind(themes, preferences, lifetime, token, size =>
            {
                if (text == null)
                {
                    throw new InvalidOperationException("The scaled TMP text was destroyed.");
                }

                if (text.enableAutoSizing)
                {
                    throw new InvalidOperationException("TMP auto-sizing conflicts with the scaled font binding.");
                }

                if (text.fontSize != size)
                {
                    text.fontSize = size;
                }
            });
        }
    }
}
