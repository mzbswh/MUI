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
            root.Add(header);
            var references = new VisualElement();
            references.AddToClassList("mui-card");
            var configuration = new PropertyField(serializedObject.FindProperty("settings"), L.Get("editor.UIHostInspector.74a883a037"));
            configuration.tooltip = L.Get("editor.UIHostInspector.6a909f3547");
            references.Add(configuration);
            var viewRoot = new PropertyField(serializedObject.FindProperty("viewRoot"), L.Get("editor.UIHostInspector.6447c4dd81"));
            viewRoot.tooltip = L.Get("editor.UIHostInspector.15bfd7c74a");
            references.Add(viewRoot);
            root.Add(references);
            var actions = new VisualElement();
            actions.AddToClassList("mui-actions");
            var dashboard = new Button(() => MUIDashboard.Show(target as UIHost));
            dashboard.AddToClassList("mui-primary-button");
            actions.Add(dashboard);
            var editSettings = new Button(() =>
            {
                var host = target as UIHost;
                if (host != null && host.Settings != null)
                {
                    Selection.activeObject = host.Settings;
                    EditorGUIUtility.PingObject(host.Settings);
                }
            });
            actions.Add(editSettings);
            root.Add(actions);

            var catalog = MUIEditorControls.Section("editor.UIHostInspector.900d3819f9", false, "MUI.Host.Catalog");
            var catalogHelp = MUIEditorControls.Text("host.catalog.help");
            catalog.Add(catalogHelp);
            var prefabs = new IMGUIContainer(() => Localization.MUIEditorInspector.Draw(serializedObject, "prefabs", false));
            catalog.Add(prefabs);
            root.Add(catalog);

            void RefreshStatus()
            {
                var host = target as UIHost;
                if (host == null)
                {
                    return;
                }
                configuration.SetEnabled(!Application.isPlaying && !host.IsInitialized);
                viewRoot.SetEnabled(!Application.isPlaying && !host.IsInitialized);
                prefabs.SetEnabled(!Application.isPlaying && !host.IsInitialized);
                editSettings.SetEnabled(host.Settings != null);
                var state = !Application.isPlaying ? L.Get("editor.UIHostInspector.20fe85fc53") : host.IsShutdown ? L.Get("editor.UIHostInspector.899df7e244") : host.IsInitialized ? L.Get("editor.UIHostInspector.c6d125d5b7") : L.Get("editor.UIHostInspector.07564e6524");
                summary.text = state;
                summary.EnableInClassList("mui-status-error", host.IsShutdown);
            }
            RefreshStatus();
            void RefreshLanguage()
            {
                configuration.label = L.Get("property.settings");
                configuration.tooltip = L.Get("editor.UIHostInspector.6a909f3547");
                viewRoot.label = L.Get("property.viewRoot");
                viewRoot.tooltip = L.Get("editor.UIHostInspector.15bfd7c74a");
                dashboard.text = L.Get("editor.UIHostInspector.652ac53466");
                editSettings.text = L.Get("host.editSettings");
                RefreshStatus();
            }
            L.Track(root, RefreshLanguage);
            RefreshLanguage();
            root.schedule.Execute(RefreshStatus).Every(500);
            return root;
        }
    }
}
