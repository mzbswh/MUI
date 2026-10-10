using System;
using System.Linq;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUIDashboard
    {
        private DropdownField languageChoices;
        private bool updatingLanguage;

        private void BindLanguage()
        {
            languageChoices = rootVisualElement.Q<DropdownField>("editorLanguage");
            languageChoices.RegisterValueChangedCallback(_ =>
            {
                var languages = L.Languages;
                if (!updatingLanguage && languageChoices.index >= 0 && languageChoices.index < languages.Count)
                {
                    L.LanguageId = languages[languageChoices.index].Id;
                }
            });
            Bind("reloadLanguages", L.ReloadLanguages);
            L.Track(rootVisualElement.Q<VisualElement>(className: "mui-dashboard"), RefreshLanguage);
            RefreshLanguage();
        }

        private void RefreshLanguage()
        {
            if (languageChoices == null)
            {
                return;
            }
            updatingLanguage = true;
            try
            {
                var languages = L.Languages;
                languageChoices.choices = languages.Select(item => item.DisplayName + " (" + item.Id + ")").ToList();
                languageChoices.index = Array.FindIndex(languages.ToArray(), item => item.Id == L.LanguageId);
                titleContent.text = L.Get("editor.MUIDashboard.788f44b794");
                rootVisualElement.Q<Button>("reloadLanguages").tooltip = L.Get("language.reload");
                languageChoices.tooltip = L.Get("language.help");
                rootVisualElement.Q<IntegerField>("snapshotLimit").tooltip = L.Get("uxml.MUIDashboard.68289cbdb5");
                rootVisualElement.Q<IntegerField>("traceCapacity").tooltip = L.Get("uxml.MUIDashboard.ee29a6789d");
            }
            finally
            {
                updatingLanguage = false;
            }
            viewDiagnostics = null;
            Capture();
            ShowTrace();
            RefreshValidationReport();
        }
    }
}
