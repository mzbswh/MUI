using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    internal static class MUIEditorControls
    {
        internal static void Style(VisualElement root, ScriptableObject source)
        {
            root.AddToClassList("mui-surface");
            root.EnableInClassList("mui-light", !EditorGUIUtility.isProSkin);
            var script = MonoScript.FromScriptableObject(source);
            var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(script));
            var editorDirectory = Path.GetDirectoryName(directory);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>((editorDirectory ?? string.Empty).Replace('\\', '/') + "/Styles/MUIEditor.uss");
            if (sheet != null && !root.styleSheets.Contains(sheet))
            {
                root.styleSheets.Add(sheet);
            }
        }

        internal static Label Text(string key, string className = "mui-note")
        {
            var label = new Label(L.Get(key));
            label.AddToClassList(className);
            L.Track(label, () => label.text = L.Get(key));
            return label;
        }

        internal static Foldout Section(string key, bool expanded, string stateKey)
        {
            var section = new Foldout { text = key == null ? string.Empty : L.Get(key), value = SessionState.GetBool(stateKey, expanded), viewDataKey = stateKey };
            section.AddToClassList("mui-card");
            section.AddToClassList("mui-section");
            section.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == section)
                {
                    SessionState.SetBool(stateKey, evt.newValue);
                }
            });
            if (key != null)
            {
                L.Track(section, () => section.text = L.Get(key));
            }
            return section;
        }

        internal static Button RemoveButton(System.Action remove)
        {
            var button = new Button(remove) { text = "\u00d7", tooltip = L.Get("settings.remove") };
            button.AddToClassList("mui-icon-button");
            L.Track(button, () => button.tooltip = L.Get("settings.remove"));
            return button;
        }
    }
}
