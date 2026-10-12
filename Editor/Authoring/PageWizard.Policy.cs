using System;
using System.Collections.Generic;
using MUI.Pages;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class PageWizard
    {
        private readonly List<string> presetNames = new List<string>();
        private PagePolicy selectedPolicy;
        private string policyError;

        private bool ReceivesInput => !createWidget && (selectedPolicy == null || selectedPolicy.ReceivesInput);

        private void OnInspectorUpdate() => Repaint();

        private PagePolicy ResolvePreset(string name) => settings == null
            ? MUISettings.ResolveBuiltInPolicy(name) : settings.ResolvePolicy(name);

        private void RefreshPolicy()
        {
            presetNames.Clear();
            selectedPolicy = null;
            policyError = settingsError;
            if (policyError != null)
                return;
            try
            {
                if (settings == null)
                    presetNames.AddRange(MUISettings.BuiltInPresetNames);
                else
                {
                    settings.RequireValid();
                    foreach (var entry in settings.Presets)
                        presetNames.Add(entry.Name);
                }
                if (string.IsNullOrEmpty(presetName))
                    presetName = settings == null ? "Default" : settings.DefaultPreset;
                if (!presetNames.Contains(presetName))
                {
                    policyError = L.Format("wizard.policyMissing", presetName);
                    return;
                }
                selectedPolicy = ResolvePreset(presetName);
            }
            catch (Exception error) { policyError = L.Diagnostic(error.Message); }
        }

        private void DrawPolicyChoices()
        {
            var selected = presetNames.IndexOf(presetName);
            var choices = presetNames.ConvertAll(name => new GUIContent(name)).ToArray();
            using (new EditorGUI.DisabledScope(choices.Length == 0))
            {
                var next = EditorGUILayout.Popup(new GUIContent(L.Get("wizard.policySelect"),
                    L.Get("wizard.policySource")), selected, choices);
                if (next >= 0 && next < presetNames.Count && next != selected)
                {
                    presetName = presetNames[next];
                    RefreshPolicy();
                    InvalidateExistingValidation();
                }
            }
            if (selectedPolicy != null)
            {
                var policy = selectedPolicy;
                var tooltip = L.Format("wizard.policyDetails", policy.CacheMode,
                    policy.AllowMultiple, policy.MaxInstances, policy.TickPause);
                GUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope(cardStyle))
                {
                    GUILayout.Label(new GUIContent(L.Get("wizard.rulesTitle"), tooltip), EditorStyles.boldLabel);
                    GUILayout.Space(8);
                    DrawPolicyRow("role", L.Value(policy.PageRole));
                    DrawPolicyRow("layer", (policy.LayerName ?? policy.Layer.ToString()) + "  (" + policy.Layer + ")");
                    DrawPolicyRow("coverage", L.Value(policy.Coverage));
                    DrawPolicyRow("modal", L.Value(policy.Modal));
                    DrawPolicyRow("back", L.Value(policy.BackBehavior));
                    DrawPolicyRow("input", L.Value(policy.ReceivesInput));
                    DrawPolicyRow("focus", L.Value(policy.TakesFocus));
                }
                GUILayout.Space(8);
                GUILayout.Label(L.Get(ReceivesInput ? "wizard.interactiveScaffold" : "wizard.passiveScaffold"), noteStyle);
                if (selectedPolicy.PageRole == PageRole.Overlay)
                    GUILayout.Label(L.Get("wizard.overlayOwner"), noteStyle);
            }
        }

        private void DrawPolicyRow(string key, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var hint = L.Get("wizard.rule." + key + "Hint");
                GUILayout.Label(new GUIContent(L.Get("wizard.rule." + key), hint), noteStyle, GUILayout.Width(150));
                GUILayout.Label(new GUIContent(value, hint), policyValueStyle, GUILayout.ExpandWidth(true));
            }
            GUILayout.Space(5);
        }
    }
}
