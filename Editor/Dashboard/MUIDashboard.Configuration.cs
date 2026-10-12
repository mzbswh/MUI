using System;
using System.Linq;
using MUI.Pages;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUIDashboard
    {
        private MUISettings displayedSettings;
        private bool settingsDisplayed;
        private Func<string> validationText;

        private void BindConfigurationAndTrace()
        {
            settingsField.objectType = typeof(MUISettings);
            settingsField.allowSceneObjects = false;
            settingsField.SetEnabled(false);
            Bind("createSettings", CreateSettings);
            Bind("locateSettings", () =>
            {
                var settings = settingsField.value as MUISettings;
                if (settings != null)
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            });
            Bind("hostSettings", ShowHostSettings);

            Bind("validateSettings", ValidateSettings);
            Bind("validateCatalogs", () => Run(() =>
            {
                var report = UIBuildValidation.Validate();
                validationText = () => report.ExportText();
                RefreshValidationReport();
            }));
            Bind("startTrace", () => RunTrace(pages => pages.StartLifecycleTrace(
                rootVisualElement.Q<IntegerField>("traceCapacity").value)));
            Bind("stopTrace", () => RunTrace(pages => pages.StopLifecycleTrace()));
            Bind("clearTrace", () => RunTrace(pages => pages.StopLifecycleTrace(clear: true)));
            Bind("captureTrace", () => RunTrace(_ => { }));
            Bind("copyTrace", () =>
            {
                if (trace != null)
                {
                    EditorGUIUtility.systemCopyBuffer = traceReport.text;
                }
            });
            Bind("copySnapshot", () =>
            {
                if (snapshot != null)
                {
                    EditorGUIUtility.systemCopyBuffer = ExportSnapshot();
                }
            });
        }

        private void ShowHostSettings()
        {
            if (settingsField == null)
            {
                return;
            }
            RefreshGlobalSettings();
            ShowSettings();
        }

        private void RefreshGlobalSettings()
        {
            RuntimeSettingsAsset.TryGet(out var settings, out var error);
            settingsField.SetValueWithoutNotify(settings);
            if (error != null)
                SetMessage(error, HelpBoxMessageType.Error);
        }

        private void ShowSettings()
        {
            RefreshGlobalSettings();
            var settings = settingsField.value as MUISettings;
            inspectedSettings = settings;
            if (settingsDisplayed && displayedSettings == settings)
            {
                UpdateActions();
                return;
            }
            settingsDisplayed = true;
            displayedSettings = settings;
            settingsInspector.Clear();
            validationText = null;
            validationReport.text = string.Empty;
            if (settings == null)
            {
                var empty = new VisualElement();
                empty.AddToClassList("mui-card");
                empty.Add(MUIEditorControls.Text("dashboard.configuration.empty", "mui-empty-title"));
                empty.Add(MUIEditorControls.Text("editor.MUIDashboard.Configuration.baf3aacd5d"));
                settingsInspector.Add(empty);
            }
            else
            {
                settingsInspector.Add(new InspectorElement(settings));
            }
            UpdateActions();
        }

        private void CreateSettings() => Run(() =>
        {
            var settings = RuntimeSettingsAsset.GetOrCreate();
            settingsField.SetValueWithoutNotify(settings);
            ShowSettings();
            EditorGUIUtility.PingObject(settings);
        });

        private void ValidateSettings() => Run(() =>
        {
            var settings = settingsField.value as MUISettings;
            if (settings == null)
            {
                validationText = () => L.Get("editor.MUIDashboard.Configuration.02c27b7054");
                RefreshValidationReport();
                return;
            }
            var errors = settings.Validate();
            validationText = () => errors.Count == 0 ? L.Get("editor.MUIDashboard.Configuration.dd9a6ee5a2") : string.Join("\n", errors.Select(L.Diagnostic));
            RefreshValidationReport();
        });

        private void RefreshValidationReport()
        {
            validationReport.text = validationText == null ? L.Get("dashboard.validation.help") : validationText();
        }

        private void RunTrace(Action<PageManager> action) => Run(() =>
        {
            if (!Application.isPlaying || selectedHost == null || !selectedHost.IsInitialized)
            {
                throw new InvalidOperationException(L.Get("editor.MUIDashboard.Configuration.d4116b321f"));
            }
            action(selectedHost.Pages);
            trace = selectedHost.Pages.CaptureLifecycleTrace();
            ShowTrace();
            UpdateActions();
        });

        private void ShowTrace()
        {
            if (traceSummary == null || traceReport == null)
            {
                return;
            }
            if (trace == null)
            {
                traceSummary.text = L.Get("editor.MUIDashboard.Configuration.f5a41d1927");
                traceReport.text = L.Get("dashboard.trace.empty");
                return;
            }
            traceSummary.text = L.Format("editor.MUIDashboard.Configuration.4b62703008", trace.IsRecording, trace.Entries.Count, trace.Capacity, trace.OverwrittenCount, trace.DroppedNotificationCount);
            var report = ExportTrace();
            var query = traceSearch.value ?? string.Empty;
            var lines = report.Split('\n');
            traceReport.text = string.IsNullOrEmpty(query) ? report : string.Join("\n", lines.Take(2).Concat(lines.Skip(2)
                .Where(line => line.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)));
        }
    }
}
