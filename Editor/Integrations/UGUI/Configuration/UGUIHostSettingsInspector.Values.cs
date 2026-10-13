using System.Collections.Generic;
using MUI.UGUI;
using MUI.Pages;
using UnityEditor;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class UGUIHostSettingsInspector
    {
        private void AddRuleGroups(VisualElement parent, string path, bool expanded)
        {
            AddRuleGroup(parent, path, "settings.rules.presentation", PagePolicyFields.PageScope | PagePolicyFields.Layer |
                PagePolicyFields.Input | PagePolicyFields.Coverage | PagePolicyFields.Focus | PagePolicyFields.Modal | PagePolicyFields.Back, expanded);
            AddRuleGroup(parent, path, "settings.rules.instances", PagePolicyFields.Instances, false);
            AddRuleGroup(parent, path, "settings.rules.tick", PagePolicyFields.Tick, false);
            AddRuleGroup(parent, path, "sorting.page", PagePolicyFields.RenderOrder, false);
            AddRuleGroup(parent, path, "settings.rules.cache", PagePolicyFields.Cache, false);
            AddRuleGroup(parent, path, "settings.rules.timeouts", PagePolicyFields.Timeouts, false);
        }

        private void AddRuleGroup(VisualElement parent, string path, string key, PagePolicyFields fields, bool expanded)
        {
            var group = Section(key, path + "." + key, expanded);
            group.RemoveFromClassList("mui-card");
            group.RemoveFromClassList("mui-section");
            group.AddToClassList("mui-rule-group");
            group.Add(new IMGUIContainer(() => DrawValues(path, fields)));
            parent.Add(group);
        }

        private void DrawValues(string path, PagePolicyFields fields)
        {
            bool Has(PagePolicyFields field) => (fields & field) != 0;
            void Draw(params string[] names)
            {
                foreach (var name in names)
                {
                    var tooltipKey = PolicyTooltipKey(name);
                    if (tooltipKey == null)
                    {
                        Localization.MUIEditorInspector.DrawFields(serializedObject, path + "." + name);
                    }
                    else
                    {
                        Localization.MUIEditorInspector.DrawField(serializedObject, path + "." + name, tooltipKey);
                    }
                }
            }
            if (Has(PagePolicyFields.PageScope))
            {
                Draw("PageRole");
                if (serializedObject.FindProperty(path + ".PageRole").intValue == (int)PageRole.Overlay)
                {
                    Draw("OwnerDeparture");
                }
            }
            if (Has(PagePolicyFields.Layer))
            {
                DrawNamedChoice(path + ".Layer", "layers", "policy.layer.help");
            }
            if (Has(PagePolicyFields.Coverage))
            {
                Draw("Coverage");
            }
            if (Has(PagePolicyFields.Input))
            {
                Draw("ReceivesInput");
            }
            if (Has(PagePolicyFields.Focus))
            {
                Draw("TakesFocus");
            }
            if (Has(PagePolicyFields.Modal))
            {
                Draw("Modal");
            }
            if (Has(PagePolicyFields.Back))
            {
                Draw("BackBehavior");
            }
            if (Has(PagePolicyFields.Instances))
            {
                Draw("AllowMultiple");
                var multiple = serializedObject.FindProperty(path + ".AllowMultiple").boolValue;
                if (multiple || serializedObject.FindProperty(path + ".MaxInstances").intValue < 1)
                {
                    Draw("MaxInstances");
                }
                if (!multiple || serializedObject.FindProperty(path + ".ExistingInstance").intValue != (int)ExistingInstancePolicy.Reject)
                {
                    Draw("ExistingInstance");
                }
                Draw("Overflow");
            }
            if (Has(PagePolicyFields.Tick))
            {
                DrawTickPause(path + ".TickPause");
            }
            if (Has(PagePolicyFields.Cache))
            {
                Draw("CacheMode");
                if (serializedObject.FindProperty(path + ".CacheMode").intValue == (int)ViewCacheMode.Timed)
                {
                    Draw("CacheDurationSeconds");
                }
            }
            if (Has(PagePolicyFields.RenderOrder))
            {
                Draw("RenderOrderSpan");
            }
            if (Has(PagePolicyFields.Timeouts))
            {
                Draw("EnterTimeout", "ExitTimeout", "PrepareTimeoutSeconds", "CloseTimeoutSeconds", "CloseDecisionTimeoutSeconds");
            }
        }

        private void DrawTickPause(string path)
        {
            if (target == null)
            {
                return;
            }
            serializedObject.UpdateIfRequiredOrScript();
            var property = serializedObject.FindProperty(path);
            if (property == null)
            {
                return;
            }
            var tooltip = L.Get("policy.tick.help");
            EditorGUILayout.LabelField(new UnityEngine.GUIContent(L.PropertyName(property), tooltip));
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();
                var value = (TickPausePolicy)property.intValue;
                var covered = EditorGUILayout.Toggle(new UnityEngine.GUIContent(L.Get("policy.tick.covered"), tooltip),
                    (value & TickPausePolicy.Covered) != 0);
                var hidden = EditorGUILayout.Toggle(new UnityEngine.GUIContent(L.Get("policy.tick.hidden"), tooltip),
                    (value & TickPausePolicy.Hidden) != 0);
                if (EditorGUI.EndChangeCheck())
                {
                    value &= ~(TickPausePolicy.Covered | TickPausePolicy.Hidden);
                    if (covered)
                    {
                        value |= TickPausePolicy.Covered;
                    }
                    if (hidden)
                    {
                        value |= TickPausePolicy.Hidden;
                    }
                    property.intValue = (int)value;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private static string PolicyTooltipKey(string name)
        {
            switch (name)
            {
                case "PageRole":
                    return "policy.pageRole.help";
                case "OwnerDeparture":
                    return "policy.ownerDeparture.help";
                case "Coverage":
                    return "policy.coverage.help";
                case "ReceivesInput":
                    return "policy.input.help";
                case "TakesFocus":
                    return "policy.focus.help";
                case "Modal":
                    return "policy.modal.help";
                case "BackBehavior":
                    return "policy.back.help";
                case "AllowMultiple":
                    return "policy.allowMultiple.help";
                case "MaxInstances":
                    return "policy.maxInstances.help";
                case "ExistingInstance":
                    return "policy.existingInstance.help";
                case "Overflow":
                    return "policy.overflow.help";
                case "TickPause":
                    return "policy.tick.help";
                case "CacheMode":
                    return "policy.cache.help";
                case "CacheDurationSeconds":
                    return "policy.cacheDuration.help";
                case "RenderOrderSpan":
                    return "sorting.span.help";
                case "EnterTimeout":
                    return "policy.EnterTimeout.help";
                case "ExitTimeout":
                    return "policy.ExitTimeout.help";
                case "PrepareTimeoutSeconds":
                    return "policy.PrepareTimeoutSeconds.help";
                case "CloseTimeoutSeconds":
                    return "policy.CloseTimeoutSeconds.help";
                case "CloseDecisionTimeoutSeconds":
                    return "policy.CloseDecisionTimeoutSeconds.help";
                default:
                    return null;
            }
        }

        private void DrawRenderSortingLayer()
        {
            if (target == null)
            {
                return;
            }
            serializedObject.UpdateIfRequiredOrScript();
            var property = serializedObject.FindProperty("renderSortingLayer");
            var layers = UnityEngine.SortingLayer.layers;
            var names = new List<string>();
            var labels = new List<UnityEngine.GUIContent>();
            foreach (var layer in layers)
            {
                names.Add(layer.name);
                labels.Add(new UnityEngine.GUIContent(layer.name));
            }
            var index = names.IndexOf(property.stringValue);
            if (index < 0)
            {
                index = labels.Count;
                labels.Add(new UnityEngine.GUIContent(L.Format("settings.choice.missing", property.stringValue)));
            }
            var label = new UnityEngine.GUIContent(L.PropertyName(property), L.Get("settings.renderSortingLayer.help"));
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.Popup(label, index, labels.ToArray());
            if (EditorGUI.EndChangeCheck() && next >= 0 && next < names.Count)
            {
                property.stringValue = names[next];
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawNamedChoice(string path, string collection, string tooltipKey = null)
        {
            if (target == null)
            {
                return;
            }
            serializedObject.UpdateIfRequiredOrScript();
            var property = serializedObject.FindProperty(path);
            if (property == null)
            {
                return;
            }
            var entries = serializedObject.FindProperty(collection);
            var names = new List<string>();
            var labels = new List<string>();
            for (var i = 0; i < entries.arraySize; ++i)
            {
                var value = entries.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue;
                if (!string.IsNullOrWhiteSpace(value) && !names.Contains(value))
                {
                    names.Add(value);
                    labels.Add(collection == "presets" ? PresetLabel(value) : value);
                }
            }
            var index = names.IndexOf(property.stringValue);
            if (index < 0)
            {
                index = names.Count;
                names.Add(property.stringValue);
                labels.Add(L.Format("settings.choice.missing", property.stringValue));
            }
            EditorGUI.BeginChangeCheck();
            var label = new UnityEngine.GUIContent(L.PropertyName(property), tooltipKey == null ? L.Diagnostic(property.tooltip) : L.Get(tooltipKey));
            var choices = System.Array.ConvertAll(labels.ToArray(), text => new UnityEngine.GUIContent(text));
            var next = EditorGUILayout.Popup(label, index, choices);
            if (EditorGUI.EndChangeCheck() && next >= 0 && next < names.Count)
            {
                property.stringValue = names[next];
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
