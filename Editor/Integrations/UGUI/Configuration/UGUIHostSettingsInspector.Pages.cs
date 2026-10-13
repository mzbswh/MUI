using MUI.Unity;
using System;
using System.Collections.Generic;
using MUI.Pages;
using MUI.Resources;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class UGUIHostSettingsInspector
    {
        private string pageFilter = string.Empty;
        private readonly Dictionary<string, HashSet<PagePolicyFields>> pageExpansions = new Dictionary<string, HashSet<PagePolicyFields>>();

        private void DrawPagePolicies()
        {
            if (target == null)
                return;
            serializedObject.UpdateIfRequiredOrScript();
            var pages = serializedObject.FindProperty("pagePolicies");
            pageFilter = EditorGUILayout.TextField(L.Get("settings.pages.search"), pageFilter);
            EditorGUILayout.LabelField(L.Get("settings.pages.count"), pages.arraySize.ToString());
            for (var i = 0; i < pages.arraySize; i++)
            {
                var entry = pages.GetArrayElementAtIndex(i);
                var key = entry.FindPropertyRelative("ResourceKey").stringValue;
                var version = entry.FindPropertyRelative("ResourceVersion").stringValue;
                if (!string.IsNullOrEmpty(pageFilter) && key.IndexOf(pageFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var identity = key + "@" + version;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, identity, true);
                        if (GUILayout.Button(L.Get("settings.pages.delete"), EditorStyles.miniButton, GUILayout.Width(52)))
                        {
                            pages.DeleteArrayElementAtIndex(i);
                            serializedObject.ApplyModifiedProperties();
                            break;
                        }
                    }
                    if (!entry.isExpanded)
                        continue;
                    Localization.MUIEditorInspector.DrawFields(serializedObject, entry.propertyPath + ".ResourceKey", entry.propertyPath + ".ResourceVersion");
                    DrawPagePrefabLink(entry);
                    PagePolicyControls.DrawPagePreset(serializedObject, entry, (MUISettings)target);
                    PagePolicy inherited = null;
                    try
                    {
                        var preset = entry.FindPropertyRelative("Preset").stringValue;
                        inherited = ((MUISettings)target).ResolvePolicy(string.IsNullOrEmpty(preset) ? null : preset).WithoutResourcePolicies();
                    }
                    catch (Exception error) { EditorGUILayout.HelpBox(L.Diagnostic(error.Message), MessageType.Error); }
                    if (!pageExpansions.TryGetValue(identity, out var expanded))
                    {
                        expanded = new HashSet<PagePolicyFields>();
                        pageExpansions.Add(identity, expanded);
                    }
                    foreach (var group in PagePolicyControls.Groups)
                        PagePolicyControls.DrawPagePolicyGroup(serializedObject, entry, (MUISettings)target,
                            group.mask, group.fields, inherited, expanded);
                }
            }
            if (GUILayout.Button(L.Get("settings.pages.add"), EditorStyles.miniButton))
            {
                var index = pages.arraySize;
                pages.InsertArrayElementAtIndex(index);
                var entry = pages.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("ResourceKey").stringValue = string.Empty;
                entry.FindPropertyRelative("ResourceVersion").stringValue = "1";
                entry.FindPropertyRelative("Preset").stringValue = string.Empty;
                entry.FindPropertyRelative("Overrides").intValue = 0;
                PagePolicyAssetSync.Link(entry, string.Empty, false);
                entry.isExpanded = true;
                pageFilter = string.Empty;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawPagePrefabLink(SerializedProperty entry)
        {
            var guid = entry.FindPropertyRelative("PrefabGuid").stringValue;
            var path = string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
            var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.ObjectField(L.Get("settings.pages.prefab"), prefab, typeof(GameObject), false) as GameObject;
            if (EditorGUI.EndChangeCheck())
            {
                var nextPath = next == null ? string.Empty : AssetDatabase.GetAssetPath(next);
                if (next == null || (next.GetComponent<View>() != null && nextPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
                {
                    PagePolicyAssetSync.Link(entry, nextPath, false);
                    serializedObject.ApplyModifiedProperties();
                }
            }
            if (string.IsNullOrEmpty(guid))
                EditorGUILayout.HelpBox(L.Get("settings.pages.unlinked"), MessageType.Info);
            else if (prefab == null)
            {
                EditorGUILayout.HelpBox(L.Get("settings.pages.missing") + "\n" + entry.FindPropertyRelative("PrefabPath").stringValue, MessageType.Warning);
                if (GUILayout.Button(L.Get("settings.pages.unlink"), EditorStyles.miniButton))
                {
                    PagePolicyAssetSync.Link(entry, string.Empty, false);
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                Localization.MUIEditorInspector.DrawFields(serializedObject, entry.propertyPath + ".ResourceKeyFromPath");
                if (entry.FindPropertyRelative("ResourceKeyFromPath").boolValue &&
                    PagePolicyAssetSync.ResourcesKey(path) != entry.FindPropertyRelative("ResourceKey").stringValue)
                    EditorGUILayout.HelpBox(L.Get("settings.pages.keyMismatch"), MessageType.Warning);
                EditorGUILayout.LabelField(L.Get("settings.pages.linked"), EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
