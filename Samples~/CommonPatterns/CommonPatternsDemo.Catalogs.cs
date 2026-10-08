using System.Collections.Generic;
using MUI.Samples.ResourceIntegration;
using MUI.Themes;

namespace MUI.Samples.CommonPatterns
{
    public sealed partial class CommonPatternsDemo
    {
        private static LocalizationCatalog CreateEnglishCatalog()
        {
            return new LocalizationCatalog("en-US", new Dictionary<string, Translation>
            {
                ["title"] = new Translation("Common UI patterns"),
                ["detail"] = new Translation("Project services update text, icons, layout and input together."),
                ["locale"] = new Translation("Français"),
                ["theme"] = new Translation("Theme"),
                ["toast"] = new Translation("Show toast"),
                ["work"] = new Translation("Run task"),
                ["working"] = new Translation("Working"),
                ["input.ready"] = new Translation("Actions available"),
                ["input.blocked"] = new Translation("Actions paused during the task"),
                ["toast.message"] = new Translation("Your changes are ready."),
                ["work.done"] = new Translation("Task complete.")
            }, count => count == 1 ? PluralCategory.One : PluralCategory.Other);
        }

        private static LocalizationCatalog CreateFrenchCatalog()
        {
            return new LocalizationCatalog("fr-FR", new Dictionary<string, Translation>
            {
                ["title"] = new Translation("Compositions UI"),
                ["detail"] = new Translation("Les services du projet actualisent le texte, les icônes, la mise en page et les entrées."),
                ["locale"] = new Translation("English"),
                ["theme"] = new Translation("Thème"),
                ["toast"] = new Translation("Afficher un avis"),
                ["work"] = new Translation("Lancer la tâche"),
                ["working"] = new Translation("Chargement"),
                ["input.ready"] = new Translation("Actions disponibles"),
                ["input.blocked"] = new Translation("Actions suspendues pendant la tâche"),
                ["toast.message"] = new Translation("Vos modifications sont prêtes."),
                ["work.done"] = new Translation("Tâche terminée.")
            }, count => count == 1 ? PluralCategory.One : PluralCategory.Other);
        }

        private static ThemeCatalog CreateTheme(bool dark)
        {
            var background = dark ? new ThemeColor(0.08f, 0.11f, 0.15f) : new ThemeColor(0.88f, 0.93f, 0.96f);
            var surface = dark ? new ThemeColor(0.15f, 0.19f, 0.24f) : new ThemeColor(1, 1, 1);
            var heading = dark ? new ThemeColor(0.97f, 0.98f, 1) : new ThemeColor(0.08f, 0.13f, 0.17f);
            var detail = dark ? new ThemeColor(0.73f, 0.82f, 0.84f) : new ThemeColor(0.25f, 0.35f, 0.39f);
            var action = dark ? new ThemeColor(0.18f, 0.63f, 0.59f) : new ThemeColor(0.08f, 0.42f, 0.39f);
            var actionText = new ThemeColor(1, 1, 1);
            var toast = dark ? new ThemeColor(0.29f, 0.46f, 0.31f) : new ThemeColor(0.77f, 0.91f, 0.75f);
            var loading = dark ? new ThemeColor(0.24f, 0.38f, 0.52f) : new ThemeColor(0.77f, 0.87f, 0.96f);
            return new ThemeCatalog(dark ? "dark" : "light", new[]
            {
                ThemeValue.Create(BackgroundToken, background),
                ThemeValue.Create(SurfaceToken, surface),
                ThemeValue.Create(HeadingToken, heading),
                ThemeValue.Create(DetailToken, detail),
                ThemeValue.Create(ActionToken, action),
                ThemeValue.Create(ActionTextToken, actionText),
                ThemeValue.Create(ToastToken, toast),
                ThemeValue.Create(LoadingToken, loading),
                ThemeValue.Create(IconToken, dark ? "dark" : "light")
            });
        }
    }
}
