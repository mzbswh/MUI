using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    [InitializeOnLoad]
    internal sealed class PagePolicyAssetSync : AssetPostprocessor
    {
        private const string PendingKey = "MUI.PagePolicyAssetSync.Pending";
        private static bool scheduled;

        [Serializable]
        private sealed class PendingChanges
        {
            public string[] Deleted = Array.Empty<string>();
        }

        static PagePolicyAssetSync()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(PendingKey, string.Empty)))
                Schedule();
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var relevant = deleted.Length != 0 || moved.Length != 0;
            foreach (var path in imported)
                relevant |= path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            if (!relevant)
                return;
            var pending = ReadPending();
            var paths = new HashSet<string>(pending.Deleted, StringComparer.Ordinal);
            foreach (var path in deleted)
                paths.Add(path);
            pending.Deleted = new List<string>(paths).ToArray();
            SessionState.SetString(PendingKey, JsonUtility.ToJson(pending));
            Schedule();
        }

        private static PendingChanges ReadPending()
        {
            var json = SessionState.GetString(PendingKey, string.Empty);
            return string.IsNullOrEmpty(json) ? new PendingChanges() : JsonUtility.FromJson<PendingChanges>(json);
        }

        private static void Schedule()
        {
            if (scheduled)
                return;
            scheduled = true;
            EditorApplication.delayCall += Synchronize;
        }

        private static void Synchronize()
        {
            scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Schedule();
                return;
            }
            var pending = ReadPending();
            SessionState.EraseString(PendingKey);
            if (!RuntimeSettingsAsset.TryGet(out var settings, out _) || settings == null)
                return;
            using (var state = new SerializedObject(settings))
            {
                var pages = state.FindProperty("pagePolicies");
                for (var i = pages.arraySize - 1; i >= 0; i--)
                {
                    var entry = pages.GetArrayElementAtIndex(i);
                    var guid = entry.FindPropertyRelative("PrefabGuid").stringValue;
                    if (string.IsNullOrEmpty(guid))
                        continue;
                    var previous = entry.FindPropertyRelative("PrefabPath").stringValue;
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                    {
                        foreach (var deleted in pending.Deleted)
                            if (!string.IsNullOrEmpty(previous) && (previous == deleted || previous.StartsWith(deleted + "/", StringComparison.Ordinal)))
                            {
                                pages.DeleteArrayElementAtIndex(i);
                                break;
                            }
                        continue;
                    }
                    if (path == previous)
                        continue;
                    entry.FindPropertyRelative("PrefabPath").stringValue = path;
                    if (!entry.FindPropertyRelative("ResourceKeyFromPath").boolValue)
                        continue;
                    var key = ResourcesKey(path);
                    if (key == null)
                        continue;
                    var version = entry.FindPropertyRelative("ResourceVersion").stringValue;
                    var duplicate = false;
                    for (var other = 0; other < pages.arraySize; other++)
                    {
                        if (other == i)
                            continue;
                        var candidate = pages.GetArrayElementAtIndex(other);
                        duplicate |= candidate.FindPropertyRelative("ResourceKey").stringValue == key &&
                            candidate.FindPropertyRelative("ResourceVersion").stringValue == version;
                    }
                    if (!duplicate)
                        entry.FindPropertyRelative("ResourceKey").stringValue = key;
                }
                if (state.ApplyModifiedProperties())
                    AssetDatabase.SaveAssetIfDirty(settings);
            }
        }

        internal static string ResourcesKey(string path)
        {
            const string marker = "/Resources/";
            var start = path.LastIndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return null;
            var key = path.Substring(start + marker.Length);
            return key.Substring(0, key.Length - System.IO.Path.GetExtension(key).Length);
        }

        internal static void LinkFromView(SerializedProperty entry, View view, bool keyFromPath)
        {
            if (!string.IsNullOrEmpty(entry.FindPropertyRelative("PrefabGuid").stringValue))
                return;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject);
            string path;
            if (stage != null && stage.prefabContentsRoot == view.gameObject)
                path = stage.assetPath;
            else
            {
                var source = EditorUtility.IsPersistent(view) ? view : PrefabUtility.GetCorrespondingObjectFromSource(view);
                if (source == null || source.transform.parent != null)
                    return;
                path = AssetDatabase.GetAssetPath(source);
            }
            Link(entry, path, keyFromPath);
        }

        internal static void Link(SerializedProperty entry, string path, bool keyFromPath)
        {
            entry.FindPropertyRelative("PrefabGuid").stringValue = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            entry.FindPropertyRelative("PrefabPath").stringValue = path;
            entry.FindPropertyRelative("ResourceKeyFromPath").boolValue = keyFromPath;
        }
    }
}
