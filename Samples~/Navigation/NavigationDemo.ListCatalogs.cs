using System.Collections.Generic;
using MUI.Samples.ResourceIntegration;
using MUI.Themes;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private static readonly ThemeToken<float> ListFontSizeToken = new ThemeToken<float>("list.fontSize");

        private static LocalizationCatalog CreateListLocale(bool french)
        {
            var translations = french
                ? new Dictionary<string, Translation>
                {
                    ["message"] = new Translation("Message traduit pour la lecture dans une liste dynamique"),
                    ["long"] = new Translation("Un message plus long occupe plusieurs lignes lorsque la fenêtre devient étroite."),
                    ["detail"] = new Translation("La liste mesure les lignes visibles et conserve la position de lecture actuelle."),
                    ["updated"] = new Translation("Le contenu a changé après la première mesure.")
                }
                : new Dictionary<string, Translation>
                {
                    ["message"] = new Translation("Message"),
                    ["long"] = new Translation("A longer message wraps to several lines when the viewport becomes narrow."),
                    ["detail"] = new Translation("The list measures visible rows and keeps the current reading position."),
                    ["updated"] = new Translation("Content changed after the first measurement.")
                };
            return new LocalizationCatalog(french ? "fr-FR" : "en-US", translations,
                count => count == 1 ? PluralCategory.One : PluralCategory.Other);
        }

        private static ThemeCatalog CreateListTheme(bool large)
        {
            return new ThemeCatalog(large ? "large-text" : "default", new[]
            {
                ThemeValue.Create(ListFontSizeToken, large ? 24f : 20f)
            });
        }
    }
}
