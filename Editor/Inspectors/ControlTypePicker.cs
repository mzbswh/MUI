using System;
using System.Collections.Generic;
using System.Linq;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    internal sealed class ControlTypePicker : PopupWindowContent
    {
        private readonly UnityEngine.Object owner;
        private readonly string path;
        private readonly string rulePattern;
        private readonly List<string> entries;
        private string query = "";
        private int filter;
        private Vector2 scroll;
        private bool focusSearch = true;
        private GUIStyle componentStyle;
        private GUIStyle selectedComponentStyle;
        private GUIStyle sourceStyle;
        private static readonly string[] common = { "Image", "RawImage", "Text", "TMP_Text", "TextMeshProUGUI", "Button", "Toggle", "Slider", "ScrollRect", "InputField", "TMP_InputField" };

        internal ControlTypePicker(UnityEngine.Object owner, string path, string rulePattern)
        {
            this.owner = owner; this.path = path; this.rulePattern = rulePattern;
            using (var serialized = new SerializedObject(owner))
            {
                var selected = Read(serialized.FindProperty(path));
                var available = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Where(type => type.IsVisible && !type.IsAbstract && !type.IsGenericType &&
                    !typeof(Element).IsAssignableFrom(type) && type != typeof(View)).ToList();
                var tmp = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", false);
                if (tmp != null) available.Add(tmp);
                entries = available.Select(type => type.FullName + ", " + type.Assembly.GetName().Name).Concat(selected)
                    .GroupBy(value => Type.GetType(value, false)?.AssemblyQualifiedName ?? value).Select(group => group.First())
                    .OrderBy(value => selected.Any(item => Same(item, value)) ? 0 : IsCommon(value) ? 1 : 2)
                    .ThenBy(value => Type.GetType(value, false)?.Name ?? value, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
        public override Vector2 GetWindowSize() => new Vector2(440, 460);
        private static bool Same(string left, string right) => left == right || Type.GetType(left, false) != null && Type.GetType(left, false) == Type.GetType(right, false);
        private static bool IsCommon(string value) => common.Contains(Type.GetType(value, false)?.Name);
        private static List<string> Read(SerializedProperty property) => property == null ? new List<string>() :
            Enumerable.Range(0, property.arraySize).Select(i => property.GetArrayElementAtIndex(i).stringValue).ToList();
        private bool DrawComponentRow(string value, Type type, bool chosen, int index)
        {
            if (componentStyle == null)
            {
                componentStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                selectedComponentStyle = new GUIStyle(componentStyle) { fontStyle = FontStyle.Bold };
                sourceStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            }
            var row = EditorGUILayout.GetControlRect(false, 27);
            var hover = row.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.MouseMove) editorWindow.Repaint();
            var dark = EditorGUIUtility.isProSkin;
            if (chosen)
                EditorGUI.DrawRect(row, dark ? new Color(0.20f, 0.40f, 0.60f, 0.65f) : new Color(0.55f, 0.76f, 0.96f));
            else if (hover || index % 2 == 0)
                EditorGUI.DrawRect(row, new Color(0.5f, 0.5f, 0.5f, hover ? 0.20f : 0.08f));
            EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1, row.width, 1), new Color(0.5f, 0.5f, 0.5f, 0.16f));
            var next = GUI.Toggle(row, chosen, new GUIContent("", value), GUIStyle.none);
            if (Event.current.type == EventType.Repaint)
                EditorStyles.toggle.Draw(new Rect(row.x + 5, row.y + 5, 18, 18), GUIContent.none, hover, false, next, false);
            var source = type == null ? L.Get("typePicker.missing") : type.Namespace == "UnityEngine.UI" ? "uGUI" :
                type.Namespace != null && type.Namespace.StartsWith("TMPro", StringComparison.Ordinal) ? "TMP" :
                type.Assembly.GetName().Name == "Assembly-CSharp" ? L.Get("typePicker.project") : type.Assembly.GetName().Name;
            var badge = new Rect(row.xMax - 100, row.y + 4, 96, row.height - 8);
            EditorGUI.DrawRect(badge, new Color(0.5f, 0.5f, 0.5f, 0.13f));
            GUI.Label(badge, new GUIContent(source, value), sourceStyle);
            var name = type == null ? value.Split(',')[0] : type.Name;
            GUI.Label(new Rect(row.x + 28, row.y, Mathf.Max(0, row.width - 132), row.height),
                new GUIContent(name, value), next ? selectedComponentStyle : componentStyle);
            return next;
        }

        public override void OnOpen() => editorWindow.wantsMouseMove = true;

        public override void OnGUI(Rect rect)
        {
            if (owner == null) { editorWindow.Close(); return; }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { editorWindow.Close(); return; }
            using (var serialized = new SerializedObject(owner))
            {
                var values = serialized.FindProperty(path);
                var prefixPath = path.Substring(0, path.LastIndexOf('.')) + ".Prefix";
                var prefix = serialized.FindProperty(prefixPath);
                if (values == null || prefix == null || prefix.stringValue != rulePattern) { editorWindow.Close(); return; }
                EditorGUILayout.LabelField(L.Get("typePicker.title"), EditorStyles.boldLabel);
                GUI.SetNextControlName("ComponentSearch");
                var nextQuery = EditorGUILayout.TextField(query, EditorStyles.toolbarSearchField);
                if (nextQuery != query) { query = nextQuery; scroll = Vector2.zero; }
                if (focusSearch) { EditorGUI.FocusTextInControl("ComponentSearch"); focusSearch = false; }
                var nextFilter = GUILayout.Toolbar(filter, new[] { L.Get("typePicker.all"), L.Get("typePicker.common"), L.Get("typePicker.selected") });
                if (nextFilter != filter) { filter = nextFilter; scroll = Vector2.zero; }
                var selected = Read(values);
                var count = 0;
                scroll = EditorGUILayout.BeginScrollView(scroll);
                foreach (var value in entries)
                {
                    var type = Type.GetType(value, false);
                    var chosen = selected.Any(item => Same(item, value));
                    if (filter == 1 && !IsCommon(value) || filter == 2 && !chosen) continue;
                    if (!string.IsNullOrWhiteSpace(query) && query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Any(word => value.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                    count++;
                    var next = DrawComponentRow(value, type, chosen, count);
                    if (next != chosen)
                    {
                        for (var i = values.arraySize - 1; i >= 0; i--)
                            if (Same(values.GetArrayElementAtIndex(i).stringValue, value)) values.DeleteArrayElementAtIndex(i);
                        if (next) values.GetArrayElementAtIndex(values.arraySize++).stringValue = value;
                        serialized.ApplyModifiedProperties();
                        editorWindow.Repaint();
                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    }
                }
                if (count == 0) EditorGUILayout.LabelField(L.Get("typePicker.empty"), EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(L.Format("typePicker.count", count, values.arraySize), EditorStyles.miniLabel);
                    if (GUILayout.Button(L.Get("typePicker.done"), GUILayout.Width(80))) editorWindow.Close();
                }
            }
        }
    }
}
