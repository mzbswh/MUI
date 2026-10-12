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
    public sealed partial class ViewInspector
    {
        private readonly HashSet<PagePolicyFields> expandedPagePolicyGroups = new HashSet<PagePolicyFields>();

        private void DrawPagePolicySource()
        {
            DrawConfigurationHint("viewPanel.policy.editHelp");
            if (manifests.Count > 1)
            {
                var next = EditorGUILayout.Popup(L.Get("editor.ViewInspector.b741b9457b"), selected + 1, labels) - 1;
                if (next != selected)
                {
                    selected = next;
                    QueueCodeBindingSync();
                    QueueBindingValidation();
                    ClearBindingTargets();
                    sourceOpenError = null;
                }
            }
            var key = FindPageResourceKey();
            if (string.IsNullOrEmpty(key))
            {
                EditorGUILayout.HelpBox(L.Get("viewPanel.policy.noResource"), MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField(L.Get("viewPanel.policy.resource"), key + "@1");
            if (!RuntimeSettingsAsset.TryGet(out var settings, out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
                return;
            }
            if (settings == null)
            {
                if (GUILayout.Button(L.Get("viewPanel.policy.create")))
                    RuntimeSettingsAsset.GetOrCreate();
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(L.Get("viewPanel.policy.storage"), settings, typeof(MUISettings), false);
                if (GUILayout.Button(L.Get("viewPanel.policy.locate"), EditorStyles.miniButton, GUILayout.Width(48)))
                    EditorGUIUtility.PingObject(settings);
            }
            using (var state = new SerializedObject(settings))
            {
                state.UpdateIfRequiredOrScript();
                var entries = state.FindProperty("pagePolicies");
                SerializedProperty entry = null;
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var item = entries.GetArrayElementAtIndex(i);
                    if (item.FindPropertyRelative("ResourceKey").stringValue == key &&
                        item.FindPropertyRelative("ResourceVersion").stringValue == "1")
                    {
                        entry = item;
                        break;
                    }
                }
                if (entry == null)
                {
                    DrawConfigurationHint("viewPanel.policy.noOverride");
                    if (GUILayout.Button(L.Get("viewPanel.policy.add")))
                    {
                        var index = entries.arraySize;
                        entries.InsertArrayElementAtIndex(index);
                        entry = entries.GetArrayElementAtIndex(index);
                        entry.FindPropertyRelative("ResourceKey").stringValue = key;
                        entry.FindPropertyRelative("ResourceVersion").stringValue = "1";
                        entry.FindPropertyRelative("Preset").stringValue = string.Empty;
                        entry.FindPropertyRelative("Overrides").intValue = 0;
                        state.ApplyModifiedProperties();
                    }
                    else
                        return;
                }
                PagePolicyAssetSync.LinkFromView(entry, (View)target, manifests.Count == 0);
                state.ApplyModifiedProperties();
                PagePolicyControls.DrawPagePreset(state, entry, settings);
                PagePolicy inherited;
                PagePolicy effective;
                try
                {
                    var preset = entry.FindPropertyRelative("Preset").stringValue;
                    var policy = settings.ResolvePolicy(string.IsNullOrEmpty(preset) ? null : preset);
                    inherited = policy.WithoutResourcePolicies();
                    effective = policy.ForResource(new ViewResource(key));
                }
                catch (Exception failure)
                {
                    EditorGUILayout.HelpBox(L.Diagnostic(failure.Message), MessageType.Error);
                    inherited = null;
                    effective = null;
                }
                DrawConfigurationHint("viewPanel.policy.overrideLegend");
                foreach (var group in PagePolicyControls.Groups)
                    PagePolicyControls.DrawPagePolicyGroup(state, entry, settings, group.mask, group.fields, inherited, expandedPagePolicyGroups);
                if (effective != null)
                    EditorGUILayout.LabelField(L.Get("viewPanel.policy.effective"),
                        L.Format("viewPanel.policy.summary", effective.LayerName ?? effective.Layer.ToString(),
                            L.Value(effective.Modal), L.Value(effective.Coverage), L.Value(effective.BackBehavior)),
                        EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(L.Get("viewPanel.policy.remove"), EditorStyles.miniButton))
                {
                    for (var i = 0; i < entries.arraySize; i++)
                        if (entries.GetArrayElementAtIndex(i).propertyPath == entry.propertyPath)
                        {
                            entries.DeleteArrayElementAtIndex(i);
                            state.ApplyModifiedProperties();
                            break;
                        }
                }
            }
        }

        private string FindPageResourceKey()
        {
            if (selected >= 0 && selected < manifests.Count)
            {
                var modelType = manifests[selected].ViewModelType;
                var page = modelType.GetCustomAttribute<UIPageAttribute>();
                if (page != null)
                    return page.GetResourceKey(modelType);
                var widget = modelType.GetCustomAttribute<UIWidgetAttribute>();
                return widget == null ? null : widget.GetResourceKey(modelType);
            }
            if (manifests.Count > 1)
                return null;
            var view = (View)target;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject);
            string path;
            if (stage != null && stage.prefabContentsRoot == view.gameObject)
                path = stage.assetPath;
            else
            {
                var source = EditorUtility.IsPersistent(view) ? view : PrefabUtility.GetCorrespondingObjectFromSource(view);
                if (source == null || source.transform.parent != null)
                    return null;
                path = AssetDatabase.GetAssetPath(source);
            }
            const string marker = "/Resources/";
            var start = path.LastIndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return null;
            var relative = path.Substring(start + marker.Length);
            return relative.Substring(0, relative.Length - System.IO.Path.GetExtension(relative).Length);
        }

    }
}
