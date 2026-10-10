using System.Linq;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    [CustomEditor(typeof(MUISettings))]
    public sealed partial class MUISettingsInspector : UnityEditor.Editor
    {
        private VisualElement root;
        private VisualElement layerRows;
        private VisualElement presetRows;
        private Label status;
        private HelpBox errors;
        private int layerCount;
        private int presetCount;
        private bool rebuildScheduled;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            root.AddToClassList("mui-inspector");
            MUIEditorControls.Style(root, this);
            var header = new VisualElement();
            header.AddToClassList("mui-row");
            var title = MUIEditorControls.Text("settings.title", "mui-title");
            title.AddToClassList("mui-grow");
            header.Add(title);
            status = new Label();
            status.AddToClassList("mui-status");
            header.Add(status);
            root.Add(header);
            root.Add(MUIEditorControls.Text("settings.description"));

            var layers = Section("property.layers", "layers", true);
            Tooltip(layers, "settings.layers.help");
            layers.Add(new IMGUIContainer(() => DrawSettingsFields("preferredOrderGap", "preferredLayerGap")));
            var columns = new VisualElement();
            columns.AddToClassList("mui-row");
            var name = MUIEditorControls.Text("property.Name", "mui-table-heading");
            name.AddToClassList("mui-layer-name");
            var order = MUIEditorControls.Text("property.Order", "mui-table-heading");
            order.AddToClassList("mui-layer-order");
            Tooltip(name, "settings.layerName.help");
            columns.Add(name);
            Tooltip(order, "settings.layerOrder.help");
            columns.Add(order);
            var gap = MUIEditorControls.Text("settings.layers.gap", "mui-table-heading");
            gap.AddToClassList("mui-layer-gap");
            Tooltip(gap, "settings.layers.gap.help");
            columns.Add(gap);
            var spacer = new VisualElement();
            spacer.AddToClassList("mui-icon-button");
            columns.Add(spacer);
            layers.Add(columns);
            layerRows = new VisualElement();
            layers.Add(layerRows);
            layers.Add(AddAction("settings.layers.add", AddLayer));
            root.Add(layers);

            var sorting = Section("sorting.title", "renderOrder", false);
            Tooltip(sorting, "sorting.help");
            sorting.Add(new IMGUIContainer(() =>
            {
                DrawRenderSortingLayer();
                DrawSettingsFields("renderOrderMinimum", "renderOrderMaximum", "defaultPageOrderSpan");
            }));
            root.Add(sorting);

            var defaults = Section("property.defaultRules", "defaultRules", false);
            Tooltip(defaults, "settings.defaults.help");
            AddRuleGroups(defaults, "defaultRules", true);
            root.Add(defaults);

            var presets = Section("property.presets", "presets", true);
            presets.Add(new IMGUIContainer(() => DrawNamedChoice("defaultPreset", "presets", "settings.defaultPreset.help")));
            Tooltip(presets, "settings.presets.help");
            presets.Add(MUIEditorControls.Text("settings.presets.help", "mui-note"));
            presetRows = new VisualElement();
            presets.Add(presetRows);
            presets.Add(AddAction("settings.presets.add", AddPreset));
            root.Add(presets);

            var runtime = Section("settings.runtime", "runtime", false);
            Tooltip(runtime, "settings.update.help");
            var automaticUpdate = new PropertyField(serializedObject.FindProperty("automaticFramePump"), L.Get("property.automaticFramePump"));
            L.Track(automaticUpdate, () => automaticUpdate.label = L.Get("property.automaticFramePump"));
            Tooltip(automaticUpdate, "settings.update.help");
            runtime.Add(automaticUpdate);
            root.Add(runtime);

            var capacities = Section("settings.capacities", "capacities", false);
            capacities.Add(new IMGUIContainer(() => DrawSettingsFields(
                "cacheCapacity", "queueCapacity", "preloadCapacity", "cleanupCapacity", "terminalCapacity")));
            root.Add(capacities);
            errors = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            root.Add(errors);
            RebuildCollections();
            root.TrackSerializedObjectValue(serializedObject, _ => OnSettingsChanged());
            L.Track(root, RefreshStatus);
            RefreshStatus();
            return root;
        }

        private static void Tooltip(VisualElement element, string key)
        {
            element.tooltip = L.Get(key);
            L.Track(element, () => element.tooltip = L.Get(key));
        }

        private void DrawSettingsFields(params string[] names)
        {
            foreach (var name in names)
            {
                Localization.MUIEditorInspector.DrawField(serializedObject, name, "settings." + name + ".help");
            }
        }

        private Foldout Section(string key, string path, bool expanded)
            => MUIEditorControls.Section(key, expanded, "MUI.Settings." + target.GetInstanceID() + "." + path);

        private static VisualElement AddAction(string key, System.Action action)
        {
            var row = new VisualElement();
            row.AddToClassList("mui-actions");
            var button = new Button(action) { text = L.Get(key) };
            L.Track(button, () => button.text = L.Get(key));
            row.Add(button);
            return row;
        }

        private void OnSettingsChanged()
        {
            if (target == null)
            {
                return;
            }
            RefreshStatus();
            if (rebuildScheduled || (layerCount == serializedObject.FindProperty("layers").arraySize &&
                presetCount == serializedObject.FindProperty("presets").arraySize))
            {
                return;
            }
            rebuildScheduled = true;
            root.schedule.Execute(() =>
            {
                rebuildScheduled = false;
                if (target != null)
                {
                    RebuildCollections();
                }
            });
        }

        private void RefreshStatus()
        {
            var settings = target as MUISettings;
            if (settings == null)
            {
                return;
            }
            var report = settings.Validate();
            status.text = report.Count == 0 ? L.Get("settings.valid") : L.Format("settings.invalid", report.Count);
            status.EnableInClassList("mui-status-error", report.Count != 0);
            errors.text = string.Join("\n", report.Select(L.Diagnostic));
            errors.style.display = report.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
