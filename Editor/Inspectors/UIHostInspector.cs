using MUI.UGUI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    [CustomEditor(typeof(UIHost))]
    public sealed class UIHostInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("mui-inspector");
            MUIEditorControls.Style(root, this);
            var header = new VisualElement();
            header.AddToClassList("mui-row");
            var title = MUIEditorControls.Text("host.title", "mui-title");
            title.AddToClassList("mui-grow");
            header.Add(title);
            var summary = new Label();
            summary.AddToClassList("mui-status");
            header.Add(summary);
            var dashboard = new Button(() => MUIDashboard.Show(target as UIHost));
            header.Add(dashboard);
            root.Add(header);
            var references = new VisualElement();
            references.AddToClassList("mui-card");
            var configuration = new ObjectField(L.Get("globalSettings.label")) { objectType = typeof(MUISettings), allowSceneObjects = false };
            configuration.SetEnabled(false);
            configuration.tooltip = L.Get("globalSettings.help");
            references.Add(configuration);
            var settingsError = new HelpBox();
            references.Add(settingsError);
            var openSettings = new Button(RuntimeSettingsAsset.OpenOrCreate);
            references.Add(openSettings);
            var viewRoot = new PropertyField(serializedObject.FindProperty("viewRoot"), L.Get("editor.UIHostInspector.6447c4dd81"));
            viewRoot.tooltip = L.Get("editor.UIHostInspector.15bfd7c74a");
            references.Add(viewRoot);
            root.Add(references);

            void RefreshStatus()
            {
                var host = target as UIHost;
                if (host == null)
                {
                    return;
                }
                var found = RuntimeSettingsAsset.TryGet(out var global, out var error);
                configuration.SetValueWithoutNotify(global);
                settingsError.text = error ?? L.Get("globalSettings.help");
                settingsError.messageType = found ? HelpBoxMessageType.Info : HelpBoxMessageType.Error;
                openSettings.text = L.Get(global == null ? "globalSettings.create" : "globalSettings.locate");
                openSettings.SetEnabled(found && !Application.isPlaying);
                viewRoot.SetEnabled(!Application.isPlaying && !host.IsInitialized);
                var state = !Application.isPlaying ? L.Get("editor.UIHostInspector.20fe85fc53") : host.IsShutdown ? L.Get("editor.UIHostInspector.899df7e244") : host.IsInitialized ? L.Get("editor.UIHostInspector.c6d125d5b7") : L.Get("editor.UIHostInspector.07564e6524");
                summary.text = state;
                summary.EnableInClassList("mui-status-error", host.IsShutdown);
            }
            RefreshStatus();
            void RefreshLanguage()
            {
                configuration.label = L.Get("globalSettings.label");
                configuration.tooltip = L.Get("globalSettings.help");
                viewRoot.label = L.Get("property.viewRoot");
                viewRoot.tooltip = L.Get("editor.UIHostInspector.15bfd7c74a");
                dashboard.text = L.Get("editor.UIHostInspector.652ac53466");
                RefreshStatus();
            }
            L.Track(root, RefreshLanguage);
            RefreshLanguage();
            root.schedule.Execute(RefreshStatus).Every(500);
            return root;
        }
    }
}
