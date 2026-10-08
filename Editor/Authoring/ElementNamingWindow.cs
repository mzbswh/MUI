using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

namespace MUI.Editor
{
    public sealed class ElementNamingWindow : EditorWindow
    {
        private View root;
        private readonly List<Entry> entries = new List<Entry>();
        private Vector2 scroll;
        private string message;

        [MenuItem("Tools/MUI/Element Naming")]
        private static void Open()
        {
            var window = GetWindow<ElementNamingWindow>("MUI Element Names");
            window.minSize = new Vector2(620, 360);
            var selected = Selection.activeGameObject;
            window.root = selected == null ? null : selected.GetComponentInParent<View>(true);
            window.Scan();
        }

        private void OnGUI()
        {
            var next = (View)EditorGUILayout.ObjectField("View", root, typeof(View), true);
            if (next != root)
            {
                root = next;
                entries.Clear();
                message = null;
            }

            EditorGUILayout.HelpBox("Rename preview only. Binding declarations refer to these names: update affected source bindings yourself. Prefixes are optional suggestions. Nested View contents are excluded; nested Prefab names are reserved but not edited. Open Prefab assets in Prefab Mode.", MessageType.Info);
            if (GUILayout.Button("Scan Names"))
            {
                Scan();
            }

            if (GUILayout.Button("Suggest Unique Names"))
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
                    EditorGUILayout.LabelField("Reserved: View root, nested Prefab, or multiple Elements on one object.");
                }
            }

            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(root == null || EditorUtility.IsPersistent(root) || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Apply Selected Names (Undo Supported)"))
                {
                    Apply();
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
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
                message = "Open the Prefab in Prefab Mode before scanning.";
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

            message = $"{entries.Count} Element objects; {empty} empty names; {repeated} repeated names. Repeated names can be valid for different concrete types; use View contract validation to check actual ambiguity.";
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
                message = "层级、组件或名字已变化，请先重新扫描。";
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
                    message = "命名期间层级、组件或名字发生变化，请重新扫描。";
                    return;
                }
                foreach (var candidate in candidates)
                {
                    candidate.Key.Proposed = candidate.Value;
                    candidate.Key.Selected = candidate.Value != candidate.Key.Original;
                }
                message = "建议尚未应用。请审核名字和勾选项后再执行重命名。";
            }
            catch (Exception error)
            {
                message = "命名规则执行失败，保留原预览：" + error.Message;
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
                message = "Hierarchy, components or names changed. Scan again before applying.";
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
                message = "No name changes selected.";
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
                    message = "Names must be non-empty, trimmed, and must not end in (Clone).";
                    return;
                }

                if (nameCounts[changed.Proposed] > 1)
                {
                    message = $"Proposed name '{changed.Proposed}' conflicts with another Element object. Nothing was renamed.";
                    return;
                }
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rename MUI Elements");
            try
            {
                foreach (var entry in changes)
                {
                    Undo.RecordObject(entry.Target, "Rename MUI Element");
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
                message = "Rename failed; reverted this batch. Inspect the Console.";
                return;
            }

            Scan();
            message = $"Renamed {changes.Count} objects. Save the scene or Prefab and update affected binding declarations. Undo restores this batch.";
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
