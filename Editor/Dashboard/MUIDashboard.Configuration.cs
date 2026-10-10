using System;
using System.Linq;
using MUI.Navigation;
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
            settingsField.RegisterValueChangedCallback(_ => ShowSettings());
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
            Bind("assignSettings", AssignSettings);
            Bind("validateSettings", ValidateSettings);
            Bind("validateCatalogs", () => Run(() =>
            {
                var report = UIBuildValidation.Validate();
                validationText = () => report.ExportText();
                RefreshValidationReport();
            }));
            Bind("startTrace", () => RunTrace(navigator => navigator.StartLifecycleTrace(
                rootVisualElement.Q<IntegerField>("traceCapacity").value)));
            Bind("stopTrace", () => RunTrace(navigator => navigator.StopLifecycleTrace()));
            Bind("clearTrace", () => RunTrace(navigator => navigator.StopLifecycleTrace(clear: true)));
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
            settingsField.SetValueWithoutNotify(selectedHost == null ? null : selectedHost.Settings);
            ShowSettings();
        }

        private void ShowSettings()
        {
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
            var path = EditorUtility.SaveFilePanelInProject(L.Get("editor.MUIDashboard.Configuration.08f8a4af53"), "MUISettings", "asset", L.Get("editor.MUIDashboard.Configuration.735d2beebb"));
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            var settings = CreateInstance<MUISettings>();
            try
            {
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
                settingsField.SetValueWithoutNotify(settings);
                ShowSettings();
                EditorGUIUtility.PingObject(settings);
            }
            finally
            {
                if (settings != null && !EditorUtility.IsPersistent(settings))
                {
                    DestroyImmediate(settings);
                }
            }
        });

        private void AssignSettings() => Run(() =>
        {
            if (selectedHost == null)
            {
                throw new InvalidOperationException(L.Get("editor.MUIDashboard.Configuration.29a1fe691e"));
            }
            if (Application.isPlaying || selectedHost.IsInitialized)
            {
                throw new InvalidOperationException(L.Get("editor.MUIDashboard.Configuration.d81da0cfb4"));
            }
            var settings = settingsField.value as MUISettings;
            if (settings != null)
            {
                settings.RequireValid();
            }
            var serialized = new SerializedObject(selectedHost);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.ApplyModifiedProperties();
            SetMessage(L.Get("editor.MUIDashboard.Configuration.a4664c33b1"), HelpBoxMessageType.Info);
            UpdateActions();
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

        private void RunTrace(Action<Navigator> action) => Run(() =>
        {
            if (!Application.isPlaying || selectedHost == null || !selectedHost.IsInitialized)
            {
                throw new InvalidOperationException(L.Get("editor.MUIDashboard.Configuration.d4116b321f"));
            }
            action(selectedHost.Navigator);
            trace = selectedHost.Navigator.CaptureLifecycleTrace();
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
