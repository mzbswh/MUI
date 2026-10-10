using System;
using System.Collections.Generic;
using System.Reflection;
using MUI.UGUI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class MUISettingsInspector
    {
        private void RebuildCollections()
        {
            serializedObject.UpdateIfRequiredOrScript();
            layerRows.Clear();
            presetRows.Clear();
            var layers = serializedObject.FindProperty("layers");
            layerCount = layers.arraySize;
            for (var i = 0; i < layerCount; ++i)
            {
                var index = i;
                var entry = layers.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.AddToClassList("mui-row");
                var name = new TextField { isDelayed = true };
                name.AddToClassList("mui-layer-name");
                name.BindProperty(entry.FindPropertyRelative("Name"));
                Tooltip(name, "settings.layerName.help");
                var order = new IntegerField { isDelayed = true };
                order.AddToClassList("mui-layer-order");
                order.BindProperty(entry.FindPropertyRelative("Order"));
                Tooltip(order, "settings.layerOrder.help");
                var gap = new IntegerField { isDelayed = true };
                gap.AddToClassList("mui-layer-gap");
                gap.BindProperty(entry.FindPropertyRelative("LayerGapAfter"));
                Tooltip(gap, "settings.layers.gap.help");
                row.Add(name);
                row.Add(order);
                row.Add(gap);
                row.Add(MUIEditorControls.RemoveButton(() => Remove("layers", index)));
                layerRows.Add(row);
            }
            var presets = serializedObject.FindProperty("presets");
            presetCount = presets.arraySize;
            for (var i = 0; i < presetCount; ++i)
            {
                AddPresetRow(presets.GetArrayElementAtIndex(i), i);
            }
            if (layerCount == 0)
            {
                layerRows.Add(MUIEditorControls.Text("settings.layers.empty"));
            }
            if (presetCount == 0)
            {
                presetRows.Add(MUIEditorControls.Text("settings.presets.empty"));
            }
            RefreshStatus();
        }

        private void AddPresetRow(SerializedProperty entry, int index)
        {
            var path = entry.propertyPath;
            var foldout = Section(null, path, false);
            foldout.RemoveFromClassList("mui-card");
            foldout.RemoveFromClassList("mui-section");
            foldout.AddToClassList("mui-preset");
            // 保留预设标识，并为内置名称补充本地化用途。
            foldout.text = string.Empty;
            var nameRow = new VisualElement();
            nameRow.AddToClassList("mui-row");
            var name = new TextField(L.Get("property.Name")) { isDelayed = true };
            name.AddToClassList("mui-grow");
            name.BindProperty(entry.FindPropertyRelative("Name"));
            Tooltip(name, "settings.presetName.help");
            nameRow.Add(name);
            nameRow.Add(MUIEditorControls.RemoveButton(() => Remove("presets", index)));
            foldout.Add(nameRow);
            foldout.Add(new IMGUIContainer(() =>
            {
                Localization.MUIEditorInspector.DrawField(serializedObject, path + ".Overrides", "settings.overrides.help");
                var mask = serializedObject.FindProperty(path + ".Overrides");
                if (mask == null)
                {
                    return;
                }
                var fields = (MUIPagePolicyFields)mask.intValue;
                if (fields == MUIPagePolicyFields.None)
                {
                    EditorGUILayout.LabelField(L.Get("settings.preset.inherit"), EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    DrawValues(path + ".Values", fields);
                }
            }));
            var effective = new Label();
            effective.AddToClassList("mui-note");
            foldout.Add(effective);
            void RefreshEffective()
            {
                var settings = target as MUISettings;
                if (settings == null)
                {
                    return;
                }
                var nameProperty = serializedObject.FindProperty(path + ".Name");
                var fieldsProperty = serializedObject.FindProperty(path + ".Overrides");
                if (nameProperty == null || fieldsProperty == null)
                {
                    return;
                }
                var presetName = nameProperty.stringValue;
                var fields = (MUIPagePolicyFields)fieldsProperty.intValue;
                try
                {
                    var policy = settings.ResolvePolicy(presetName);
                    string Value(string property, object value, MUIPagePolicyFields field, bool automatic = false)
                        => L.Format("settings.preset.value", L.Get("property." + property), value,
                            L.Get(automatic ? "settings.preset.source.modal" :
                                (fields & field) != 0 ? "settings.preset.source.override" : "settings.preset.source.default"));
                    var rawCoverage = (fields & MUIPagePolicyFields.Coverage) != 0
                        ? serializedObject.FindProperty(path + ".Values.Coverage").intValue
                        : serializedObject.FindProperty("defaultRules.Coverage").intValue;
                    effective.text = string.Join("\n", new[]
                    {
                        L.Get("settings.preset.effective"),
                        Value("PageRole", policy.PageRole, MUIPagePolicyFields.PageScope),
                        Value("Layer", policy.LayerName, MUIPagePolicyFields.Layer),
                        Value("ReceivesInput", policy.ReceivesInput, MUIPagePolicyFields.Input),
                        Value("Coverage", policy.Coverage, MUIPagePolicyFields.Coverage,
                            policy.Modal && rawCoverage == (int)MUI.Navigation.CoveragePolicy.None),
                        Value("TakesFocus", policy.TakesFocus, MUIPagePolicyFields.Focus),
                        Value("Modal", policy.Modal, MUIPagePolicyFields.Modal),
                        Value("BackBehavior", policy.BackBehavior, MUIPagePolicyFields.Back)
                    });
                }
                catch (Exception)
                {
                    effective.text = L.Get("settings.preset.invalid");
                }
            }
            effective.TrackSerializedObjectValue(serializedObject, _ => RefreshEffective());
            L.Track(effective, RefreshEffective);
            RefreshEffective();
            void RefreshTitle()
            {
                if (target == null)
                {
                    return;
                }
                var property = serializedObject.FindProperty(path + ".Name");
                var value = property == null ? string.Empty : property.stringValue;
                foldout.text = string.IsNullOrWhiteSpace(value) ? L.Get("settings.preset.unnamed") : PresetLabel(value);
                name.label = L.Get("property.Name");
            }
            foldout.TrackPropertyValue(entry.FindPropertyRelative("Name"), _ => RefreshTitle());
            L.Track(foldout, RefreshTitle);
            RefreshTitle();
            presetRows.Add(foldout);
        }

        private static string PresetLabel(string value)
        {
            switch (value)
            {
                case "Default":
                case "FullScreen":
                case "Popup":
                case "Notice":
                case "Background":
                    return value + " · " + L.Get("settings.preset." + value);
                default:
                    return value;
            }
        }

        private void Remove(string collection, int index)
        {
            serializedObject.Update();
            var list = serializedObject.FindProperty(collection);
            if (index < list.arraySize)
            {
                list.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                RebuildCollections();
            }
        }

        private void AddLayer()
        {
            serializedObject.Update();
            var layers = serializedObject.FindProperty("layers");
            var name = UniqueName(layers, "Layer");
            var used = new HashSet<int>();
            var largest = -100;
            for (var i = 0; i < layers.arraySize; ++i)
            {
                var order = layers.GetArrayElementAtIndex(i).FindPropertyRelative("Order").intValue;
                used.Add(order);
                largest = Math.Max(largest, order);
            }
            var next = largest <= int.MaxValue - 100 ? largest + 100 : 0;
            while (used.Contains(next))
            {
                ++next;
            }
            var index = layers.arraySize++;
            var entry = layers.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Name").stringValue = name;
            entry.FindPropertyRelative("Order").intValue = next;
            entry.FindPropertyRelative("LayerGapAfter").intValue = -1;
            serializedObject.ApplyModifiedProperties();
            RebuildCollections();
        }

        private void AddPreset()
        {
            serializedObject.Update();
            var presets = serializedObject.FindProperty("presets");
            var name = UniqueName(presets, "Preset");
            var index = presets.arraySize++;
            var entry = presets.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Name").stringValue = name;
            entry.FindPropertyRelative("Overrides").intValue = (int)MUIPagePolicyFields.None;
            // Unity 扩容可能复制上一项，显式恢复新预设的规则初值。
            var values = entry.FindPropertyRelative("Values");
            var defaults = new MUIPagePolicyValues();
            foreach (var field in typeof(MUIPagePolicyValues).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var property = values.FindPropertyRelative(field.Name);
                var value = field.GetValue(defaults);
                switch (property.propertyType)
                {
                    case SerializedPropertyType.String: property.stringValue = (string)value; break;
                    case SerializedPropertyType.Boolean: property.boolValue = (bool)value; break;
                    case SerializedPropertyType.Float: property.floatValue = (float)value; break;
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Enum: property.intValue = Convert.ToInt32(value); break;
                }
            }
            serializedObject.ApplyModifiedProperties();
            SessionState.SetBool("MUI.Settings." + target.GetInstanceID() + "." + entry.propertyPath, true);
            RebuildCollections();
        }

        private static string UniqueName(SerializedProperty list, string prefix)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < list.arraySize; ++i)
            {
                names.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue);
            }
            var suffix = 1;
            while (names.Contains(prefix + suffix))
            {
                ++suffix;
            }
            return prefix + suffix;
        }
    }
}
