using System;
using System.Collections.Generic;
using System.Reflection;
using MUI.Pages;
using MUI.Resources;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    internal static class PagePolicyControls
    {
        internal static readonly (PagePolicyFields mask, string[] fields)[] Groups =
        {
            (PagePolicyFields.PageScope, new[] { "PageRole", "OwnerDeparture" }),
            (PagePolicyFields.Layer, new[] { "Layer" }),
            (PagePolicyFields.Coverage, new[] { "Coverage" }),
            (PagePolicyFields.Input, new[] { "ReceivesInput" }),
            (PagePolicyFields.Focus, new[] { "TakesFocus" }),
            (PagePolicyFields.Modal, new[] { "Modal" }),
            (PagePolicyFields.Back, new[] { "BackBehavior" }),
            (PagePolicyFields.Instances, new[] { "AllowMultiple", "MaxInstances", "ExistingInstance", "Overflow" }),
            (PagePolicyFields.Tick, new[] { "TickPause" }),
            (PagePolicyFields.RenderOrder, new[] { "RenderOrderSpan" }),
            (PagePolicyFields.Cache, new[] { "CacheMode", "CacheDurationSeconds" }),
            (PagePolicyFields.Timeouts, new[] { "EnterTimeout", "ExitTimeout", "PrepareTimeoutSeconds", "CloseTimeoutSeconds", "CloseDecisionTimeoutSeconds" })
        };

        internal static void DrawPagePreset(SerializedObject state, SerializedProperty entry, MUISettings settings)
        {
            var choices = new List<string> { L.Get("viewPanel.policy.inheritPreset") };
            var preset = entry.FindPropertyRelative("Preset");
            var index = 0;
            foreach (var item in settings.Presets)
            {
                choices.Add(item.Name);
                if (item.Name == preset.stringValue)
                    index = choices.Count - 1;
            }
            if (!string.IsNullOrEmpty(preset.stringValue) && index == 0)
            {
                choices.Add(preset.stringValue);
                index = choices.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.Popup(L.Get("viewPanel.policy.preset"), index, choices.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                preset.stringValue = next == 0 ? string.Empty : choices[next];
                state.ApplyModifiedProperties();
            }
        }

        internal static void DrawPagePolicyGroup(SerializedObject state, SerializedProperty entry, MUISettings settings,
            PagePolicyFields mask, string[] fields, PagePolicy inherited, HashSet<PagePolicyFields> expandedPagePolicyGroups)
        {
            var overrides = entry.FindPropertyRelative("Overrides");
            var enabled = (overrides.intValue & (int)mask) != 0;
            var multiple = fields.Length > 1;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var next = EditorGUILayout.Toggle(new GUIContent(string.Empty, L.Get("viewPanel.policy.overrideLegend")),
                    enabled, GUILayout.Width(18));
                if (EditorGUI.EndChangeCheck())
                {
                    if (next && inherited != null)
                        foreach (var field in fields)
                            SetPolicyValue(entry.FindPropertyRelative("Values." + field), PolicyValue(inherited, field));
                    overrides.intValue = next ? overrides.intValue | (int)mask : overrides.intValue & ~(int)mask;
                    state.ApplyModifiedProperties();
                    enabled = next;
                    if (next && multiple)
                        expandedPagePolicyGroups.Add(mask);
                }
                if (!multiple)
                    DrawPagePolicyField(state, entry, settings, fields[0], inherited, enabled);
                else
                {
                    var expanded = EditorGUILayout.Foldout(expandedPagePolicyGroups.Contains(mask), L.Value(mask), true);
                    if (expanded)
                        expandedPagePolicyGroups.Add(mask);
                    else
                        expandedPagePolicyGroups.Remove(mask);
                    GUILayout.Label(L.Get(enabled ? "viewPanel.policy.localValue" : "viewPanel.policy.inheritedValue"),
                        EditorStyles.miniLabel, GUILayout.Width(64));
                }
            }
            if (!multiple || !expandedPagePolicyGroups.Contains(mask))
                return;
            using (new EditorGUI.IndentLevelScope())
                foreach (var field in fields)
                {
                    object Value(string name) => enabled
                        ? (object)entry.FindPropertyRelative("Values." + name).intValue
                        : inherited == null ? null : PolicyValue(inherited, name);
                    if (field == "OwnerDeparture" && Value("PageRole") != null &&
                        Convert.ToInt32(Value("PageRole")) != (int)PageRole.Overlay)
                        continue;
                    if (field == "CacheDurationSeconds" && Value("CacheMode") != null &&
                        Convert.ToInt32(Value("CacheMode")) != (int)ViewCacheMode.Timed)
                        continue;
                    DrawPagePolicyField(state, entry, settings, field, inherited, enabled);
                }
        }

        private static void DrawPagePolicyField(SerializedObject state, SerializedProperty entry, MUISettings settings,
            string field, PagePolicy inherited, bool enabled)
        {
            var property = entry.FindPropertyRelative("Values." + field);
            if (!enabled)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.LabelField(L.Get("property." + field), inherited == null
                        ? L.Get("viewPanel.policy.invalidPreview") : L.Value(PolicyValue(inherited, field)));
                return;
            }
            if (field == "Layer")
            {
                var names = new List<string>();
                foreach (var layer in settings.Layers)
                    names.Add(layer.Name);
                var index = names.IndexOf(property.stringValue);
                if (index < 0)
                {
                    names.Add(property.stringValue);
                    index = names.Count - 1;
                }
                EditorGUI.BeginChangeCheck();
                var selectedLayer = EditorGUILayout.Popup(L.Get("property.Layer"), index, names.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    property.stringValue = names[selectedLayer];
                    state.ApplyModifiedProperties();
                }
            }
            else
                Localization.MUIEditorInspector.DrawFields(state, property.propertyPath);
        }

        private static void SetPolicyValue(SerializedProperty property, object value)
        {
            if (value is bool boolean)
                property.boolValue = boolean;
            else if (value is string text)
                property.stringValue = text;
            else if (value is float number)
                property.floatValue = number;
            else
                property.intValue = Convert.ToInt32(value);
        }

        private static object PolicyValue(PagePolicy policy, string field)
        {
            switch (field)
            {
                case "Layer":
                    return policy.LayerName ?? "Default";
                case "Coverage":
                    return policy.RequestedCoverage;
                case "CacheDurationSeconds":
                    return (float)(policy.CacheDuration.HasValue ? policy.CacheDuration.Value.TotalSeconds : 60);
                case "PrepareTimeoutSeconds":
                    return (float)policy.PrepareTimeout.TotalSeconds;
                case "CloseTimeoutSeconds":
                    return (float)policy.CloseTimeout.TotalSeconds;
                case "CloseDecisionTimeoutSeconds":
                    return (float)policy.CloseDecisionTimeout.TotalSeconds;
                default:
                    return typeof(PagePolicy).GetProperty(field).GetValue(policy);
            }
        }
    }
}
