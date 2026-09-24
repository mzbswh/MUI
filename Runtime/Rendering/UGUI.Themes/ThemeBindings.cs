using System;
using MUI.Themes;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public static class ThemeBindings
    {
        /// <summary>在本生命周期内独占 Graphic.color 写入，不要再绑定或高亮同一个颜色属性。</summary>
        public static IDisposable BindColor(ThemeService themes, Lifetime lifetime, Graphic graphic, ThemeToken<ThemeColor> token)
        {
            if (themes == null)
            {
                throw new ArgumentNullException(nameof(themes));
            }

            if (graphic == null)
            {
                throw new ArgumentNullException(nameof(graphic));
            }

            return themes.Observe(lifetime, token, color =>
            {
                if (graphic == null)
                {
                    throw new InvalidOperationException("The themed Graphic was destroyed.");
                }

                var next = ToColor(color);
                if (graphic.color != next)
                {
                    graphic.color = next;
                }
            });
        }

        public static IDisposable BindSpacing(ThemeService themes,
            Lifetime lifetime,
            HorizontalOrVerticalLayoutGroup layout,
            ThemeToken<float> token)
        {
            if (themes == null)
            {
                throw new ArgumentNullException(nameof(themes));
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            return themes.Observe(lifetime, token, value =>
            {
                if (layout == null)
                {
                    throw new InvalidOperationException("The themed layout was destroyed.");
                }

                if (layout.spacing != value)
                {
                    layout.spacing = value;
                }
            });
        }

        public static IDisposable BindSelectableColors(ThemeService themes,
            Lifetime lifetime,
            Selectable selectable,
            ThemeToken<ThemeStateColors> token)
        {
            if (themes == null)
            {
                throw new ArgumentNullException(nameof(themes));
            }

            if (selectable == null)
            {
                throw new ArgumentNullException(nameof(selectable));
            }

            if (selectable.transition != Selectable.Transition.ColorTint)
            {
                throw new InvalidOperationException("Selectable theme colors require a ColorTint transition.");
            }

            return themes.Observe(lifetime, token, value =>
            {
                if (selectable == null)
                {
                    throw new InvalidOperationException("The themed Selectable was destroyed.");
                }

                if (selectable.transition != Selectable.Transition.ColorTint)
                {
                    throw new InvalidOperationException("Selectable transition changed away from ColorTint.");
                }

                var colors = selectable.colors;
                colors.normalColor = ToColor(value.Normal);
                colors.highlightedColor = ToColor(value.Highlighted);
                colors.pressedColor = ToColor(value.Pressed);
                colors.selectedColor = ToColor(value.Selected);
                colors.disabledColor = ToColor(value.Disabled);
                if (!selectable.colors.Equals(colors))
                {
                    selectable.colors = colors;
                }
            });
        }

        private static Color ToColor(ThemeColor color) => new Color(color.Red, color.Green, color.Blue, color.Alpha);
    }
}
