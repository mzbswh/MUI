using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed class ElementNamingWindow : EditorWindow
    {
        private View root;
        private readonly List<Entry> entries = new List<Entry>();
        private Vector2 scroll;
        private string message;

        [MenuItem("Tools/MUI/控件命名 (Element Naming)")]
        private static void Open()
        {
            var window = GetWindow<ElementNamingWindow>(L.Get("editor.ElementNamingWindow.358bb9962f"));
            window.minSize = new Vector2(620, 360);
            var selected = Selection.activeGameObject;
            window.root = selected == null ? null : selected.GetComponentInParent<View>(true);
            window.Scan();
        }

        private void OnGUI()
        {
            titleContent.text = L.Get("editor.ElementNamingWindow.358bb9962f");
            var next = (View)EditorGUILayout.ObjectField(L.Get("editor.ElementNamingWindow.dcc839a401"), root, typeof(View), true);
            if (next != root)
            {
                root = next;
                entries.Clear();
                message = null;
            }

            EditorGUILayout.HelpBox(L.Get("editor.ElementNamingWindow.281ad19dde"), MessageType.Info);
            if (GUILayout.Button(L.Get("editor.ElementNamingWindow.20ca39694d")))
            {
                Scan();
            }

            if (GUILayout.Button(L.Get("editor.ElementNamingWindow.c3d72457bf")))
            {
                Suggest();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var entry in entries)
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(!entry.Editable || entry.Target == null))
                {
                    entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(20));
                    EditorGUILayout.ObjectField(entry.Target, typeof(GameObject), true);
                    entry.Proposed = EditorGUILayout.TextField(entry.Proposed);
                }

                EditorGUILayout.EndHorizontal();
                if (!entry.Editable)
                {
                    EditorGUILayout.LabelField(L.Get("editor.ElementNamingWindow.ae93df9c64"));
                }
            }

            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(root == null || EditorUtility.IsPersistent(root) || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(L.Get("editor.ElementNamingWindow.3ac473e9b6")))
                {
                    Apply();
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(L.Diagnostic(message), MessageType.Info);
            }
        }

        private void Scan()
        {
            entries.Clear();
            message = null;
            if (root == null)
            {
                return;
            }

            if (EditorUtility.IsPersistent(root))
            {
                message = L.Get("editor.ElementNamingWindow.a5e3077f3f");
                return;
            }

            Collect(root.transform, true, false, entries);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var repeated = 0;
            var empty = 0;
            foreach (var entry in entries)
            {
                var name = ElementNaming.BindingName(entry.Original);
                if (string.IsNullOrWhiteSpace(name))
                {
                    ++empty;
                }

                if (!names.Add(name))
                {
                    ++repeated;
                }
            }

            message = L.Format("editor.ElementNamingWindow.ef6c4a64c8", entries.Count, empty, repeated);
        }

        private void Collect(Transform node, bool isRoot, bool protectedPrefab, List<Entry> result)
        {
            if (!isRoot && node.GetComponent<View>() != null)
            {
                return;
            }

            protectedPrefab |= !isRoot && PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject);
            var elements = node.GetComponents<Element>();
            if (elements.Length > 0)
            {
                result.Add(new Entry
                {
                    Target = node.gameObject,
                    Elements = elements,
                    Parent = node.parent,
                    SiblingIndex = node.GetSiblingIndex(),
                    Original = node.name,
                    Proposed = node.name,
                    Editable = !isRoot && !protectedPrefab && elements.Length == 1
                });
            }

            foreach (var component in node.GetComponents<MonoBehaviour>())
            {
                if (component is IElementBoundary)
                {
                    return;
                }
            }

            for (var i = 0; i < node.childCount; ++i)
            {
                Collect(node.GetChild(i), false, protectedPrefab, result);
            }
        }

        private void Suggest()
        {
            if (root == null || entries.Count == 0)
            {
                return;
            }

            if (!IsCurrent())
            {
                message = L.Get("editor.ElementNamingWindow.98e9dc4d39");
                return;
            }

            var reserved = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<KeyValuePair<Entry, string>>();
            foreach (var entry in entries)
            {
                if (!entry.Editable)
                {
                    reserved.Add(ElementNaming.BindingName(entry.Original));
                }
            }

            try
            {
                foreach (var entry in entries)
                {
                    if (!entry.Editable || entry.Target == null || entry.Elements[0] == null)
                    {
                        continue;
                    }

                    var basis = ElementNaming.Suggest(entry.Original, entry.Elements[0].GetType());
                    var name = basis;
                    var suffix = 2;
                    while (!reserved.Add(name))
                    {
                        name = basis + "_" + suffix++;
                    }
                    candidates.Add(new KeyValuePair<Entry, string>(entry, name));
                }

                // 规则由项目提供，完成后重新检查快照，避免发布过期或半批建议。
                if (root == null || !IsCurrent())
                {
                    message = L.Get("editor.ElementNamingWindow.25f9c635ef");
                    return;
                }
                foreach (var candidate in candidates)
                {
                    candidate.Key.Proposed = candidate.Value;
                    candidate.Key.Selected = candidate.Value != candidate.Key.Original;
                }
                message = L.Get("editor.ElementNamingWindow.046d5fd2e9");
            }
            catch (Exception error)
            {
                message = L.Get("editor.ElementNamingWindow.8f31895d55") + error.Message;
            }
        }

        private bool IsCurrent()
        {
            var current = new List<Entry>();
            Collect(root.transform, true, false, current);
            if (current.Count != entries.Count)
            {
                return false;
            }

            for (var i = 0; i < current.Count; ++i)
            {
                var before = entries[i];
                var after = current[i];
                // 重挂载可能保持扫描顺序不变，不能仅凭对象与名称相同接受旧预览。
                if (before.Target != after.Target || before.Parent != after.Parent ||
                    before.SiblingIndex != after.SiblingIndex || before.Original != after.Original ||
                    before.Editable != after.Editable || before.Elements.Length != after.Elements.Length)
                {
                    return false;
                }

                for (var j = 0; j < before.Elements.Length; ++j)
                {
                    if (before.Elements[j] != after.Elements[j])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void Apply()
        {
            if (root == null || EditorUtility.IsPersistent(root) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!IsCurrent())
            {
                message = L.Get("editor.ElementNamingWindow.66a01cf520");
                return;
            }

            var changes = new List<Entry>();
            foreach (var entry in entries)
            {
                if (entry.Editable && entry.Selected && entry.Original != entry.Proposed)
                {
                    changes.Add(entry);
                }
            }

            if (changes.Count == 0)
            {
                message = L.Get("editor.ElementNamingWindow.2c1f2dca62");
                return;
            }

            var changedEntries = new HashSet<Entry>(changes);
            var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var finalName = ElementNaming.BindingName(changedEntries.Contains(entry) ? entry.Proposed : entry.Original);
                nameCounts.TryGetValue(finalName, out var count);
                nameCounts[finalName] = count + 1;
            }

            foreach (var changed in changes)
            {
                if (string.IsNullOrWhiteSpace(changed.Proposed) || changed.Proposed != changed.Proposed.Trim() || changed.Proposed != ElementNaming.BindingName(changed.Proposed))
                {
                    message = L.Get("editor.ElementNamingWindow.668fbf85fb");
                    return;
                }

                if (nameCounts[changed.Proposed] > 1)
                {
                    message = L.Format("editor.ElementNamingWindow.5fb53e10b3", changed.Proposed);
                    return;
                }
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(L.Get("editor.ElementNamingWindow.d985880acf"));
            try
            {
                foreach (var entry in changes)
                {
                    Undo.RecordObject(entry.Target, L.Get("editor.ElementNamingWindow.f1eab560bb"));
                    entry.Target.name = entry.Proposed;
                    if (PrefabUtility.IsPartOfPrefabInstance(entry.Target))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(entry.Target);
                    }
                }

                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
            }
            catch (Exception error)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
                Scan();
                message = L.Get("editor.ElementNamingWindow.bae8c2d3dc");
                return;
            }

            Scan();
            message = L.Format("editor.ElementNamingWindow.553f3cc103", changes.Count);
        }

        private sealed class Entry
        {
            internal GameObject Target;
            internal Element[] Elements;
            internal Transform Parent;
            internal int SiblingIndex;
            internal string Original;
            internal string Proposed;
            internal bool Editable;
            internal bool Selected;
        }
    }
}
